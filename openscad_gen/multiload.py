from __future__ import annotations

from dataclasses import dataclass, replace
import logging
from typing import Iterable

import numpy as np

from .fem import FemContext, FemResult, solve_density_fem
from .models import VoxelScene


@dataclass(slots=True)
class LoadCase:
    """One FEM load vector for a shared geometry/stiffness design."""

    name: str
    force_vector: np.ndarray
    weight: float = 1.0


@dataclass(slots=True)
class MultiLoadResult:
    success: bool
    reason: str
    cases: dict[str, FemResult]
    weighted_compliance: float
    max_displacement: float
    max_connector_vm: float
    aggregated_design_energy: np.ndarray | None


def solve_load_cases(
    voxel_scene: VoxelScene,
    ctx: FemContext,
    rho_phys: np.ndarray,
    penal: float,
    cases: Iterable[LoadCase],
    logger: logging.Logger,
    emin_ratio: float = 1.0e-6,
    aggregation: str = "weighted_sum",
) -> MultiLoadResult:
    """Solve several load cases on the same topology.

    The existing FEM context is reused. Only the force vector is replaced per case, so mesh/DOF
    construction does not repeat. `aggregated_design_energy` can be fed into a future multi-load
    sensitivity update; weighted sum is the standard compliance aggregation baseline.
    """
    case_list = list(cases)
    if not case_list:
        raise ValueError("At least one load case is required")

    results: dict[str, FemResult] = {}
    energies: list[tuple[float, np.ndarray]] = []
    weighted_compliance = 0.0
    max_displacement = 0.0
    max_vm = 0.0

    for case in case_list:
        force = np.asarray(case.force_vector, dtype=float).reshape(-1)
        if force.shape != ctx.force_vector.shape:
            raise ValueError(
                f"Load case '{case.name}' has {force.size} DOFs, expected {ctx.force_vector.size}"
            )
        local_ctx = replace(ctx, force_vector=force)
        result = solve_density_fem(
            voxel_scene,
            local_ctx,
            rho_phys,
            penal=penal,
            logger=logger,
            emin_ratio=emin_ratio,
        )
        results[case.name] = result
        if not result.success:
            return MultiLoadResult(
                success=False,
                reason=f"load case {case.name}: {result.reason}",
                cases=results,
                weighted_compliance=float("inf"),
                max_displacement=float("inf"),
                max_connector_vm=float("inf"),
                aggregated_design_energy=None,
            )

        weight = max(0.0, float(case.weight))
        weighted_compliance += weight * float(result.compliance)
        max_displacement = max(max_displacement, float(result.max_displacement))
        max_vm = max(max_vm, float(result.connector_max_vm))
        if result.design_ce is not None:
            energies.append((weight, np.asarray(result.design_ce, dtype=float)))

    aggregate: np.ndarray | None = None
    if energies:
        if aggregation == "max":
            aggregate = np.maximum.reduce([e for _, e in energies])
        elif aggregation == "weighted_sum":
            aggregate = np.zeros_like(energies[0][1])
            for weight, energy in energies:
                aggregate += weight * energy
        else:
            raise ValueError("aggregation must be 'weighted_sum' or 'max'")

    return MultiLoadResult(
        success=True,
        reason="ok",
        cases=results,
        weighted_compliance=float(weighted_compliance),
        max_displacement=float(max_displacement),
        max_connector_vm=float(max_vm),
        aggregated_design_energy=aggregate,
    )


def scaled_load_cases(ctx: FemContext, scales: dict[str, float]) -> list[LoadCase]:
    """Convenience helper for parametric load sweeps around the scene's nominal load."""
    return [
        LoadCase(name=name, force_vector=np.asarray(ctx.force_vector) * float(scale))
        for name, scale in scales.items()
    ]
