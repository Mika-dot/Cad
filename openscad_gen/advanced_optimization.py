from __future__ import annotations

from dataclasses import dataclass
import math
from typing import Iterable, Sequence

import numpy as np
from scipy.ndimage import binary_dilation, distance_transform_edt


@dataclass(slots=True, frozen=True)
class ContinuationState:
    """Numerically stable continuation parameters for density topology optimization."""

    penal: float
    beta: float
    move_limit: float


@dataclass(slots=True, frozen=True)
class ContinuationSchedule:
    """Continuation policy for SIMP penalization, Heaviside projection and OC move limits.

    The schedule is iteration-driven and deterministic.  It deliberately keeps beta low while
    the topology is still moving, then sharpens the design only after the SIMP exponent has
    approached its target.  This reduces early binary locking and checkerboard-like artifacts.
    """

    penal_start: float = 1.5
    penal_max: float = 3.5
    penal_step: float = 0.5
    penal_every: int = 6
    beta_start: float = 1.0
    beta_max: float = 16.0
    beta_scale: float = 2.0
    beta_every: int = 8
    move_start: float = 0.20
    move_min: float = 0.05
    move_decay: float = 0.85
    move_every: int = 8

    def state(self, iteration: int) -> ContinuationState:
        i = max(0, int(iteration))
        p_steps = i // max(1, self.penal_every)
        b_steps = i // max(1, self.beta_every)
        m_steps = i // max(1, self.move_every)
        penal = min(self.penal_max, self.penal_start + p_steps * self.penal_step)
        beta = min(self.beta_max, self.beta_start * self.beta_scale**b_steps)
        move = max(self.move_min, self.move_start * self.move_decay**m_steps)
        return ContinuationState(float(penal), float(beta), float(move))


def heaviside_projection(
    rho_tilde: np.ndarray,
    beta: float,
    eta: float,
) -> tuple[np.ndarray, np.ndarray]:
    """Smooth Heaviside projection and derivative with respect to filtered density."""

    x = np.asarray(rho_tilde, dtype=float)
    beta = max(float(beta), 1.0e-8)
    eta = float(np.clip(eta, 1.0e-6, 1.0 - 1.0e-6))
    denom = np.tanh(beta * eta) + np.tanh(beta * (1.0 - eta))
    y = (np.tanh(beta * eta) + np.tanh(beta * (x - eta))) / denom
    dy = beta * (1.0 - np.tanh(beta * (x - eta)) ** 2) / denom
    return y, dy


def robust_projection_triplet(
    rho_tilde: np.ndarray,
    beta: float,
    eta: float = 0.5,
    delta: float = 0.10,
) -> dict[str, tuple[np.ndarray, np.ndarray]]:
    """Return eroded, nominal and dilated density realizations.

    This is the standard three-field idea used in robust density topology optimization.  The
    three realizations approximate manufacturing under-/nominal-/over-etch (or print-width)
    variation without changing the design variables themselves.
    """

    delta = max(0.0, float(delta))
    return {
        "eroded": heaviside_projection(rho_tilde, beta, min(0.999, eta + delta)),
        "nominal": heaviside_projection(rho_tilde, beta, eta),
        "dilated": heaviside_projection(rho_tilde, beta, max(0.001, eta - delta)),
    }


def ks_aggregate(values: Sequence[float] | np.ndarray, rho: float = 40.0) -> float:
    """Stable Kreisselmeier-Steinhauser smooth maximum.

    Useful for displacement/stress/load-case constraints where a hard max is non-smooth.
    For large ``rho`` the result approaches max(values).
    """

    x = np.asarray(values, dtype=float).reshape(-1)
    if x.size == 0:
        return 0.0
    r = max(float(rho), 1.0e-9)
    xmax = float(np.max(x))
    return float(xmax + math.log(float(np.exp(r * (x - xmax)).sum())) / r)


def pnorm_aggregate(values: Sequence[float] | np.ndarray, p: float = 8.0) -> float:
    """p-norm aggregation for non-negative local constraints."""

    x = np.maximum(np.asarray(values, dtype=float).reshape(-1), 0.0)
    if x.size == 0:
        return 0.0
    p = max(float(p), 1.0)
    scale = max(float(np.max(x)), 1.0e-30)
    return float(scale * (np.mean((x / scale) ** p) ** (1.0 / p)))


def aggregate_element_energies(
    energies: Iterable[np.ndarray],
    weights: Sequence[float] | None = None,
    mode: str = "weighted_sum",
    p: float = 8.0,
) -> np.ndarray:
    """Aggregate per-element strain energies from several load cases.

    ``weighted_sum`` is the classic multi-load compliance objective; ``max`` targets the worst
    case directly; ``pnorm`` provides a smooth approximation that remains differentiable enough
    for gradient-based updates.
    """

    arrays = [np.asarray(e, dtype=float) for e in energies]
    if not arrays:
        raise ValueError("At least one energy array is required")
    shape = arrays[0].shape
    if any(a.shape != shape for a in arrays):
        raise ValueError("All load-case energy arrays must have identical shape")

    if weights is None:
        w = np.ones(len(arrays), dtype=float)
    else:
        w = np.asarray(weights, dtype=float)
        if w.size != len(arrays):
            raise ValueError("weights length must match energy arrays")
        w = np.maximum(w, 0.0)
    if float(w.sum()) <= 0.0:
        w[:] = 1.0
    w /= float(w.sum())

    stack = np.stack(arrays, axis=0)
    if mode == "weighted_sum":
        return np.tensordot(w, stack, axes=(0, 0))
    if mode == "max":
        return np.max(stack, axis=0)
    if mode == "pnorm":
        p = max(float(p), 1.0)
        scale = np.maximum(np.max(stack, axis=0), 1.0e-30)
        normalized = stack / scale
        return scale * np.sum(w[:, None] * normalized**p, axis=0) ** (1.0 / p)
    raise ValueError("mode must be 'weighted_sum', 'max' or 'pnorm'")


def oc_update(
    density: np.ndarray,
    objective_sensitivity: np.ndarray,
    volume_sensitivity: np.ndarray,
    target_mass: float,
    move_limit: float,
    min_density: float = 1.0e-3,
    fixed_solid: np.ndarray | None = None,
    max_bisection: int = 100,
) -> np.ndarray:
    """Generalized Optimality-Criteria update with a filtered volume derivative.

    The implementation accepts arbitrary positive volume sensitivities and fixed-solid design
    variables, making it reusable for density filters, robust projections and multi-load cases.
    """

    x = np.asarray(density, dtype=float).copy()
    dc = np.asarray(objective_sensitivity, dtype=float)
    dv = np.maximum(np.asarray(volume_sensitivity, dtype=float), 1.0e-14)
    if x.shape != dc.shape or x.shape != dv.shape:
        raise ValueError("density and sensitivities must have identical shape")

    fixed = np.zeros_like(x, dtype=bool) if fixed_solid is None else np.asarray(fixed_solid, dtype=bool)
    free = ~fixed
    x[fixed] = 1.0
    target = max(float(target_mass), float(np.count_nonzero(fixed)))
    move = max(float(move_limit), 1.0e-6)

    lo, hi = 0.0, 1.0e12
    candidate = x.copy()
    for _ in range(max(1, int(max_bisection))):
        lam = max(0.5 * (lo + hi), 1.0e-30)
        ratio = np.maximum(1.0e-30, -dc[free] / (dv[free] * lam))
        free_new = x[free] * np.sqrt(ratio)
        free_new = np.clip(
            free_new,
            np.maximum(min_density, x[free] - move),
            np.minimum(1.0, x[free] + move),
        )
        candidate = x.copy()
        candidate[free] = free_new
        candidate[fixed] = 1.0
        if float(candidate.sum()) > target:
            lo = lam
        else:
            hi = lam
        if (hi - lo) / max(hi + lo, 1.0) < 1.0e-8:
            break
    return candidate


def overhang_violation_mask(
    solid: np.ndarray,
    build_axis: int = 2,
    build_sign: int = 1,
    support_radius: int = 1,
) -> np.ndarray:
    """Cheap voxel AM overhang diagnostic.

    A voxel is considered supported when material exists in the previous build layer within a
    Chebyshev radius.  ``support_radius=1`` corresponds to a one-voxel lateral support cone and
    is intentionally conservative.  This is a diagnostic/penalty mask, not a slicer replacement.
    """

    vox = np.asarray(solid, dtype=bool)
    if vox.ndim != 3:
        raise ValueError("solid must be a 3-D boolean array")
    axis = int(build_axis)
    if axis not in (0, 1, 2):
        raise ValueError("build_axis must be 0, 1 or 2")
    sign = 1 if int(build_sign) >= 0 else -1
    radius = max(0, int(support_radius))

    prev = np.roll(vox, shift=sign, axis=axis)
    boundary_index = 0 if sign > 0 else -1
    sl = [slice(None)] * 3
    sl[axis] = boundary_index
    prev[tuple(sl)] = False

    if radius > 0:
        structure = np.ones((2 * radius + 1,) * 3, dtype=bool)
        # Do not let dilation jump into another build layer.
        center = radius
        for i in range(structure.shape[axis]):
            if i != center:
                slicer = [slice(None)] * 3
                slicer[axis] = i
                structure[tuple(slicer)] = False
        prev = binary_dilation(prev, structure=structure)

    first_layer = np.zeros_like(vox, dtype=bool)
    sl[axis] = 0 if sign > 0 else -1
    first_layer[tuple(sl)] = vox[tuple(sl)]
    return vox & ~prev & ~first_layer


def minimum_feature_violation(
    solid: np.ndarray,
    minimum_radius_voxels: float,
) -> np.ndarray:
    """Flag solid voxels that do not satisfy a minimum inscribed-radius target."""

    vox = np.asarray(solid, dtype=bool)
    if minimum_radius_voxels <= 0:
        return np.zeros_like(vox, dtype=bool)
    inside_distance = distance_transform_edt(vox)
    return vox & (inside_distance < float(minimum_radius_voxels))
