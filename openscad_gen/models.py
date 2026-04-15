from __future__ import annotations

from dataclasses import dataclass, field, asdict
from pathlib import Path
from typing import Any


@dataclass(slots=True)
class Material:
    name: str
    young_modulus: float
    poisson_ratio: float
    density: float
    yield_strength: float
    color: str = "#999999"

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


@dataclass(slots=True)
class Primitive:
    kind: str
    params: dict[str, Any]
    translate: tuple[float, float, float] = (0.0, 0.0, 0.0)


@dataclass(slots=True)
class Entity:
    name: str
    role: str
    material: str
    primitive: Primitive
    fix: tuple[int, int, int] = (0, 0, 0)
    force: tuple[float, float, float] = (0.0, 0.0, 0.0)
    connect: bool = False
    structural: bool = True
    preserve: bool = False
    avoid: bool = False


@dataclass(slots=True)
class SceneConfig:
    connector_material: str = "petg"
    voxel_size: float = 2.0
    bbox_margin: float = 8.0
    safety_factor: float = 2.0
    target_volume_ratio: float = 0.18
    max_displacement: float = 3.0
    max_iterations: int = 45
    render_every: int = 4
    min_connector_neighbors: int = 1
    filter_radius: float = 2.5
    move_limit: float = 0.15
    min_density: float = 1.0e-3
    void_stiffness_ratio: float = 1.0e-6
    solver: str = "auto"
    cg_relative_tolerance: float = 1.0e-8
    cg_max_iterations: int = 4000
    large_system_relative_tolerance: float = 1.0e-6
    large_system_max_iterations: int = 12000
    solver_diagonal_regularization: float = 1.0e-9
    penal_start: float = 1.5
    penal_max: float = 3.5
    penal_step: float = 0.5
    penal_every: int = 6
    projection_beta_start: float = 2.0
    projection_beta_max: float = 12.0
    projection_beta_scale: float = 1.5
    projection_eta: float = 0.45
    density_threshold: float = 0.30
    post_smooth_passes: int = 1
    final_resolve_max_dofs: int = 250000
    max_active_dofs: int = 350000
    max_estimated_matrix_gb: float = 6.0
    practical_iterative_max_dofs: int = 1200000
    force_allow_large_problems: bool = True
    matrix_free_large_systems: bool = True
    matrix_free_min_dofs: int = 600000
    matrix_free_min_estimated_matrix_gb: float = 2.5
    matrix_free_chunk_size: int = 4096
    fallback_to_matrix_free_on_oom: bool = True

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


@dataclass(slots=True)
class Scene:
    config: SceneConfig
    variables: dict[str, float]
    entities: list[Entity]
    path: Path


@dataclass(slots=True)
class GridSpec:
    origin: tuple[float, float, float]
    voxel_size: float
    shape: tuple[int, int, int]

    @property
    def nx(self) -> int:
        return self.shape[0]

    @property
    def ny(self) -> int:
        return self.shape[1]

    @property
    def nz(self) -> int:
        return self.shape[2]


@dataclass(slots=True)
class VoxelScene:
    grid: GridSpec
    anchor_mask: Any
    load_mask: Any
    obstacle_mask: Any
    preserve_mask: Any
    design_mask: Any
    mandatory_design_mask: Any
    connector_mask: Any
    domain_mask: Any
    entity_masks: dict[str, Any]
    scene: Scene


@dataclass(slots=True)
class IterationMetrics:
    iteration: int
    removal_fraction: float
    active_connector_voxels: int
    connector_volume: float
    volume_ratio: float
    max_connector_vm: float
    max_abs_vm: float
    max_displacement: float
    compliance: float
    accepted: bool
    reason: str

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


DEFAULT_MATERIALS: dict[str, Material] = {
    # В проекте OpenSCAD геометрия задаётся в миллиметрах, поэтому и механика здесь считается в N/mm^2 (МПа),
    # а не в Па. Иначе жёсткость завышается примерно в 10^6 раз, и оптимизатор почти не видит деформации.
    "abs": Material(
        name="abs",
        young_modulus=2100.0,
        poisson_ratio=0.35,
        density=1.04e-6,
        yield_strength=40.0,
        color="#4c78a8",
    ),
    "petg": Material(
        name="petg",
        young_modulus=2000.0,
        poisson_ratio=0.38,
        density=1.27e-6,
        yield_strength=38.0,
        color="#f58518",
    ),
}
