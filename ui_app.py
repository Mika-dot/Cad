from __future__ import annotations

import json
import math
import threading
import time
import traceback
import uuid
from pathlib import Path
from typing import Any

import numpy as np
import pandas as pd
import plotly.express as px
import plotly.graph_objects as go
import streamlit as st

from openscad_gen.exporters import export_structured_scene
from openscad_gen.fem import estimate_fem_requirements
from openscad_gen.geometry import build_voxel_scene, cell_centers_from_grid, entity_bbox, point_mask_for_primitive
from openscad_gen.models import DEFAULT_MATERIALS, Entity, Scene, SceneConfig
from openscad_gen.parser import parse_auto_scene
from openscad_gen.pipeline import run_pipeline

ROOT = Path(__file__).resolve().parent
WORK_ROOT = ROOT / "ui_runs"
WORK_ROOT.mkdir(parents=True, exist_ok=True)
ROLE_OPTIONS = ["anchor", "load", "obstacle", "part", "preserve"]
MATERIAL_OPTIONS = list(DEFAULT_MATERIALS.keys())
ROLE_COLORS = {
    "anchor": "#4c78a8",
    "load": "#54a24b",
    "obstacle": "#9d755d",
    "preserve": "#72b7b2",
    "part": "#8c8c8c",
}
QUALITY_PRESETS = {
    "Черновик": {"render_every": 6, "max_iterations": 24, "filter_radius": 2.0, "bbox_margin": 6.0},
    "Баланс": {"render_every": 4, "max_iterations": 40, "filter_radius": 2.5, "bbox_margin": 8.0},
    "Детально": {"render_every": 3, "max_iterations": 50, "filter_radius": 3.0, "bbox_margin": 10.0},
}

st.set_page_config(page_title="OpenSCAD Gen UI", page_icon="🧊", layout="wide")
st.markdown(
    """
    <style>
    .stApp { background: #f7f8fa; color: #1f2937; }
    .block-container { padding-top: 1.0rem; padding-bottom: 2rem; }
    h1, h2, h3 { color: #111827; }
    .role-chip {
        display: inline-block;
        padding: 0.2rem 0.55rem;
        margin: 0.05rem 0.3rem 0.2rem 0;
        border-radius: 999px;
        font-size: 0.86rem;
        font-weight: 600;
        background: #eef2ff;
        color: #374151;
    }
    </style>
    """,
    unsafe_allow_html=True,
)


def scene_to_dict(scene: Scene) -> dict[str, Any]:
    return {
        "config": scene.config.to_dict(),
        "variables": scene.variables,
        "entities": [
            {
                "name": e.name,
                "role": e.role,
                "material": e.material,
                "primitive": {"kind": e.primitive.kind, "params": e.primitive.params, "translate": list(e.primitive.translate)},
                "fix": list(e.fix),
                "force": list(e.force),
                "connect": e.connect,
                "structural": e.structural,
                "preserve": e.preserve,
                "avoid": e.avoid,
            }
            for e in scene.entities
        ],
        "path": str(scene.path),
    }


def dict_to_scene(payload: dict[str, Any]) -> Scene:
    from openscad_gen.models import Primitive

    entities = []
    for item in payload["entities"]:
        prim = item["primitive"]
        entities.append(
            Entity(
                name=item["name"],
                role=item["role"],
                material=item["material"],
                primitive=Primitive(kind=prim["kind"], params=prim["params"], translate=tuple(float(v) for v in prim["translate"])),
                fix=tuple(int(v) for v in item["fix"]),
                force=tuple(float(v) for v in item["force"]),
                connect=bool(item["connect"]),
                structural=bool(item["structural"]),
                preserve=bool(item["preserve"]),
                avoid=bool(item["avoid"]),
            )
        )
    return Scene(config=SceneConfig(**payload["config"]), variables=payload.get("variables", {}), entities=entities, path=Path(payload.get("path", "scene.scad")))


def apply_role_defaults(entity: dict[str, Any]) -> None:
    role = entity["role"]
    if role == "anchor":
        entity["connect"] = True
        entity["structural"] = True
        entity["preserve"] = True
        entity["avoid"] = False
    elif role == "load":
        entity["connect"] = True
        entity["structural"] = True
        entity["preserve"] = True
        entity["avoid"] = False
    elif role == "obstacle":
        entity["connect"] = False
        entity["structural"] = False
        entity["preserve"] = False
        entity["avoid"] = True
    elif role == "preserve":
        entity["connect"] = False
        entity["structural"] = True
        entity["preserve"] = True
        entity["avoid"] = False
    else:
        entity["structural"] = True


def sample_entity_points(entity: Entity, density: int = 9) -> tuple[list[float], list[float], list[float]]:
    kind = entity.primitive.kind
    tx, ty, tz = entity.primitive.translate
    params = entity.primitive.params
    xs: list[float] = []
    ys: list[float] = []
    zs: list[float] = []

    if kind == "compound":
        mn, mx = entity_bbox(entity)
        nx = max(8, density)
        ny = max(8, density)
        nz = max(8, density)
        xs_lin = np.linspace(mn[0], mx[0], nx)
        ys_lin = np.linspace(mn[1], mx[1], ny)
        zs_lin = np.linspace(mn[2], mx[2], nz)
        xx, yy, zz = np.meshgrid(xs_lin, ys_lin, zs_lin, indexing="ij")
        pts = np.stack([xx, yy, zz], axis=-1)
        mask = point_mask_for_primitive(pts, entity.primitive)
        if mask.any():
            selected = pts[mask]
            if len(selected) > 3500:
                step = max(1, len(selected) // 3500)
                selected = selected[::step]
            return selected[:, 0].tolist(), selected[:, 1].tolist(), selected[:, 2].tolist()
        return [], [], []

    if kind == "cube":
        size = params.get("size", 1.0)
        if isinstance(size, (int, float)):
            sx = sy = sz = float(size)
        else:
            sx, sy, sz = [float(v) for v in size]
        center = bool(params.get("center", False))
        x0 = tx - sx / 2 if center else tx
        y0 = ty - sy / 2 if center else ty
        z0 = tz - sz / 2 if center else tz
        for ix in range(density):
            for iy in range(density):
                for iz in range(density):
                    x = x0 + sx * ix / max(density - 1, 1)
                    y = y0 + sy * iy / max(density - 1, 1)
                    z = z0 + sz * iz / max(density - 1, 1)
                    if ix in (0, density - 1) or iy in (0, density - 1) or iz in (0, density - 1):
                        xs.append(x)
                        ys.append(y)
                        zs.append(z)
    elif kind == "sphere":
        r = float(params.get("r", 1.0))
        for ia in range(18):
            phi = 2 * math.pi * ia / 18
            for ib in range(9):
                theta = math.pi * ib / 8
                xs.append(tx + r * math.sin(theta) * math.cos(phi))
                ys.append(ty + r * math.sin(theta) * math.sin(phi))
                zs.append(tz + r * math.cos(theta))
    elif kind == "cylinder":
        r = float(params.get("r", 1.0))
        h = float(params.get("h", 1.0))
        center = bool(params.get("center", False))
        z_min = tz - h / 2 if center else tz
        z_max = tz + h / 2 if center else tz + h
        for ia in range(24):
            phi = 2 * math.pi * ia / 24
            cx = tx + r * math.cos(phi)
            cy = ty + r * math.sin(phi)
            for iz in range(density):
                z = z_min + (z_max - z_min) * iz / max(density - 1, 1)
                xs.append(cx)
                ys.append(cy)
                zs.append(z)
    return xs, ys, zs


def build_geometry_figure(scene: Scene, voxel_mode: bool) -> go.Figure:
    fig = go.Figure()
    if voxel_mode:
        try:
            voxel_scene = build_voxel_scene(scene)
            centers = cell_centers_from_grid(voxel_scene.grid)
            for entity in scene.entities:
                mask = voxel_scene.entity_masks.get(entity.name)
                if mask is None or not mask.any():
                    continue
                pts = centers[mask]
                fig.add_trace(
                    go.Scatter3d(
                        x=pts[:, 0],
                        y=pts[:, 1],
                        z=pts[:, 2],
                        mode="markers",
                        name=entity.name,
                        marker=dict(size=4.2, color=ROLE_COLORS.get(entity.role, "#8c8c8c"), opacity=0.82),
                    )
                )
        except Exception:
            voxel_mode = False
    if not voxel_mode:
        for entity in scene.entities:
            xs, ys, zs = sample_entity_points(entity)
            fig.add_trace(
                go.Scatter3d(
                    x=xs,
                    y=ys,
                    z=zs,
                    mode="markers",
                    name=entity.name,
                    marker=dict(size=3.2, color=ROLE_COLORS.get(entity.role, "#8c8c8c"), opacity=0.82),
                )
            )
    fig.update_layout(
        template="plotly_white",
        height=620,
        margin=dict(l=0, r=0, t=32, b=0),
        scene=dict(
            aspectmode="data",
            xaxis_title="X",
            yaxis_title="Y",
            zaxis_title="Z",
            bgcolor="rgba(0,0,0,0)",
            camera=dict(eye=dict(x=1.4, y=1.35, z=0.9)),
        ),
        legend=dict(orientation="h", yanchor="bottom", y=1.02, x=0.0),
    )
    return fig


def read_json(path: Path) -> dict[str, Any] | None:
    if not path.exists():
        return None
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return None


def _safe_scene_estimate(scene: Scene) -> dict[str, Any] | None:
    try:
        voxel_scene = build_voxel_scene(scene)
        return estimate_fem_requirements(voxel_scene)
    except Exception:
        return None


def _apply_quality_preset(cfg: dict[str, Any], preset_name: str) -> None:
    preset = QUALITY_PRESETS[preset_name]
    for key, value in preset.items():
        cfg[key] = value


def _role_summary(scene: Scene) -> dict[str, int]:
    counts: dict[str, int] = {role: 0 for role in ROLE_OPTIONS}
    for entity in scene.entities:
        counts[entity.role] = counts.get(entity.role, 0) + 1
    return counts


def _human_int(value: int | float) -> str:
    return f"{int(value):,}".replace(",", " ")




def _estimate_over_limit(cfg: dict[str, Any], estimate: dict[str, Any] | None) -> tuple[bool, str | None, float | None]:
    if estimate is None:
        return False, None, None
    max_dofs = max(float(cfg.get("max_active_dofs", 350000)), 1.0)
    max_gb = max(float(cfg.get("max_estimated_matrix_gb", 6.0)), 0.1)
    practical_iter_dofs = max(float(cfg.get("practical_iterative_max_dofs", 1200000)), 1.0)
    ndofs = float(estimate.get("ndofs", 0.0))
    matrix_gb = float(estimate.get("estimated_matrix_gb", 0.0))
    dof_ratio = ndofs / max_dofs
    mem_ratio = matrix_gb / max_gb
    practical_ratio = ndofs / practical_iter_dofs
    worst_ratio = max(dof_ratio, mem_ratio, practical_ratio)
    if worst_ratio <= 1.0:
        return False, None, None
    current_voxel = max(float(cfg.get("voxel_size", 1.0)), 1e-6)
    suggested = current_voxel * (worst_ratio ** (1.0 / 3.0)) * 1.08
    if practical_ratio >= max(dof_ratio, mem_ratio):
        reason = (
            f"Оценка FEM упирается в практический предел итерационного решателя: "
            f"DOFs {_human_int(int(ndofs))} / {_human_int(int(practical_iter_dofs))}."
        )
    else:
        reason = (
            f"Оценка FEM превышает лимиты: DOFs {_human_int(int(ndofs))} / {_human_int(int(max_dofs))}, "
            f"матрица {matrix_gb:.1f} / {max_gb:.1f} GiB."
        )
    return True, reason, round(suggested, 2)

def render_monitor(run_dir: Path, thread: threading.Thread | None = None, result_holder: dict[str, Any] | None = None) -> None:
    status_box = st.empty()
    metrics_box = st.empty()
    chart_box = st.empty()
    col_img_1, col_img_2 = st.columns(2)
    img_box_1 = col_img_1.empty()
    img_box_2 = col_img_2.empty()
    log_box = st.empty()
    refresh_idx = 0

    while True:
        refresh_idx += 1
        status = read_json(run_dir / "status.json") or {}
        metrics_path = run_dir / "metrics_live.csv"
        logs = (run_dir / "run.log").read_text(encoding="utf-8") if (run_dir / "run.log").exists() else ""
        state = status.get("state", "running")

        with status_box.container():
            if state == "failed":
                st.error(f"Оптимизация завершилась с ошибкой: {status.get('reason', 'неизвестная ошибка')}")
            elif state == "completed":
                st.success(f"Готово. Результаты: {run_dir}")
            else:
                st.info("Оптимизация выполняется…")

            c1, c2, c3, c4 = st.columns(4)
            c1.metric("Состояние", state)
            c2.metric("Фаза", status.get("phase", "-"))
            c3.metric("Итерация", status.get("iteration", 0))
            c4.metric("Обновлено", status.get("updated_at", "-"))
            if status.get("reason"):
                st.caption(f"Причина: {status['reason']}")

        if metrics_path.exists():
            df = pd.read_csv(metrics_path)
            metrics_box.dataframe(df.tail(12), width="stretch", hide_index=True)
            if not df.empty:
                fig = go.Figure()
                fig.add_trace(go.Scatter(x=df["iteration"], y=df["volume_ratio"], mode="lines+markers", name="volume_ratio"))
                fig.add_trace(go.Scatter(x=df["iteration"], y=df["max_displacement"], mode="lines+markers", name="max_displacement"))
                fig.update_layout(template="plotly_white", height=360, title="Ход оптимизации", margin=dict(l=16, r=16, t=48, b=16))
                chart_box.plotly_chart(fig, width="stretch", key=f"live_chart_{run_dir.name}_{refresh_idx}")
        else:
            metrics_box.empty()
            chart_box.empty()

        latest_frame = status.get("latest_frame")
        latest_stress = status.get("latest_stress_frame")
        if latest_frame and Path(latest_frame).exists():
            img_box_1.image(str(latest_frame), caption="Текущий шаг топологии", width="stretch")
        else:
            img_box_1.empty()
        if latest_stress and Path(latest_stress).exists():
            img_box_2.image(str(latest_stress), caption="Карта напряжений по вокселям", width="stretch")
        else:
            img_box_2.empty()

        if result_holder and result_holder.get("error"):
            log_box.code(result_holder["error"], language="text")
        else:
            log_box.code("\n".join(logs.splitlines()[-24:]) if logs else "Лог пока пуст.", language="text")

        if thread is None or not thread.is_alive():
            break
        time.sleep(1.2)

    summary_path = run_dir / "summary.json"
    if summary_path.exists() and state == "completed":
        with st.expander("Итоговая сводка", expanded=True):
            st.json(json.loads(summary_path.read_text(encoding="utf-8")))

    gif_path = run_dir / "animation.gif"
    if gif_path.exists() and state == "completed":
        st.image(str(gif_path), caption="Анимация шагов", width="stretch")

    download_cols = st.columns(5)
    for idx, artifact in enumerate(["final_connector.scad", "final_scene_preview.scad", "metrics.csv", "summary.json", "animation.gif"]):
        p = run_dir / artifact
        if p.exists():
            download_cols[idx].download_button(f"Скачать {artifact}", data=p.read_bytes(), file_name=artifact, width="stretch")


def ensure_session_id() -> str:
    if "session_uid" not in st.session_state:
        st.session_state.session_uid = uuid.uuid4().hex[:8]
    return st.session_state.session_uid


def main() -> None:
    st.title("OpenSCAD → оптимизация коннектора")
    st.caption("Загрузите `.scad`, проверьте роли объектов, затем запустите оптимизацию. 3D-предпросмотр можно вращать мышкой.")

    session_uid = ensure_session_id()
    work_dir = WORK_ROOT / session_uid
    work_dir.mkdir(parents=True, exist_ok=True)

    top_left, top_right = st.columns([1.4, 0.6])
    with top_left:
        uploaded = st.file_uploader("Исходный `.scad`", type=["scad"], help="Файл пользователя будет разобран и преобразован в структурированную сцену.")
    with top_right:
        reset_scene = st.button("Сбросить сцену", width="stretch")

    if reset_scene:
        for key in ["scene_payload", "source_name", "last_run_dir", "ui_preset"]:
            st.session_state.pop(key, None)

    if uploaded is not None:
        saved_path = work_dir / uploaded.name
        saved_path.write_bytes(uploaded.getvalue())
        scene = parse_auto_scene(saved_path)
        st.session_state.scene_payload = scene_to_dict(scene)
        st.session_state.source_name = uploaded.name

    if "scene_payload" not in st.session_state:
        st.info("Загрузите `.scad`. После загрузки появятся быстрые настройки, интерактивный 3D-предпросмотр, оценка сложности и запуск оптимизации.")
        return

    payload = st.session_state.scene_payload
    scene = dict_to_scene(payload)
    cfg = payload["config"]

    with st.sidebar:
        st.markdown("## Быстрые настройки")
        preset_default = st.session_state.get("ui_preset", "Баланс")
        preset = st.radio("Пресет качества", list(QUALITY_PRESETS.keys()), index=list(QUALITY_PRESETS.keys()).index(preset_default))
        if st.button("Применить пресет", width="stretch"):
            _apply_quality_preset(cfg, preset)
            st.session_state.ui_preset = preset
        st.session_state.ui_preset = preset

        cfg["connector_material"] = st.selectbox("Материал коннектора", MATERIAL_OPTIONS, index=MATERIAL_OPTIONS.index(cfg["connector_material"]))
        cfg["voxel_size"] = st.number_input("Размер вокселя", min_value=0.1, value=float(cfg["voxel_size"]), step=0.1)
        cfg["target_volume_ratio"] = st.slider("Целевой объём", min_value=0.05, max_value=0.95, value=float(cfg["target_volume_ratio"]), step=0.01)
        cfg["max_displacement"] = st.number_input("Макс. перемещение", min_value=0.01, value=float(cfg["max_displacement"]), step=0.1)
        cfg["safety_factor"] = st.number_input("Коэффициент запаса", min_value=1.0, value=float(cfg["safety_factor"]), step=0.1)

        with st.expander("Расширенные параметры"):
            cfg["bbox_margin"] = st.number_input("Отступ bbox", min_value=0.0, value=float(cfg["bbox_margin"]), step=1.0)
            cfg["filter_radius"] = st.slider("Радиус фильтра", min_value=1.0, max_value=6.0, value=float(cfg.get("filter_radius", 2.5)), step=0.25)
            cfg["move_limit"] = st.slider("Move limit", min_value=0.02, max_value=0.35, value=float(cfg.get("move_limit", 0.15)), step=0.01)
            cfg["penal_max"] = st.slider("Макс. penal", min_value=2.0, max_value=5.0, value=float(cfg.get("penal_max", 3.0)), step=0.25)
            cfg["density_threshold"] = st.slider("Порог плотности финальной геометрии", min_value=0.2, max_value=0.8, value=float(cfg.get("density_threshold", 0.45)), step=0.05)
            cfg["max_iterations"] = st.number_input("Макс. итераций", min_value=1, value=int(cfg["max_iterations"]), step=1)
            cfg["render_every"] = st.number_input("Рендерить каждый N-й шаг", min_value=1, value=int(cfg["render_every"]), step=1)
            solver_options = ["auto", "cg", "direct", "matrix_free"]
            current_solver = str(cfg.get("solver", "auto")).lower()
            cfg["solver"] = st.selectbox("Решатель", solver_options, index=solver_options.index(current_solver) if current_solver in solver_options else 0, help="auto = assembled/direct для умеренных задач и matrix-free/cg для больших; direct может быть очень прожорливым по памяти, matrix_free экономит RAM ценой скорости.")
            cfg["max_active_dofs"] = st.number_input("Мягкий предел активных DOFs", min_value=50000, value=int(cfg.get("max_active_dofs", 350000)), step=25000, help="Используется для предупреждения и блокировки до подтверждения, но не как абсолютный запрет.")
            cfg["max_estimated_matrix_gb"] = st.number_input("Мягкий предел матрицы, GiB", min_value=1.0, value=float(cfg.get("max_estimated_matrix_gb", 6.0)), step=0.5, help="Это только предварительная оценка. Реальный расход памяти direct-решателя может быть сильно выше.")
            cfg["practical_iterative_max_dofs"] = st.number_input("Мягкий предел DOFs для итерационного решателя", min_value=100000, value=int(cfg.get("practical_iterative_max_dofs", 1200000)), step=50000, help="Нужен для предупреждения перед очень тяжёлыми запусками с solver=auto/cg.")
            cfg["matrix_free_large_systems"] = st.checkbox("Авто-переход на matrix-free для больших задач", value=bool(cfg.get("matrix_free_large_systems", True)), help="Не собирает и не факторизует глобальную матрицу целиком; работает медленнее, но намного экономнее по RAM.")
            cfg["matrix_free_min_dofs"] = st.number_input("Порог DOFs для matrix-free", min_value=100000, value=int(cfg.get("matrix_free_min_dofs", 600000)), step=50000)
            cfg["matrix_free_min_estimated_matrix_gb"] = st.number_input("Порог оценки матрицы для matrix-free, GiB", min_value=0.5, value=float(cfg.get("matrix_free_min_estimated_matrix_gb", 2.5)), step=0.5)
            cfg["matrix_free_chunk_size"] = st.number_input("Chunk size matrix-free", min_value=256, value=int(cfg.get("matrix_free_chunk_size", 4096)), step=256, help="Меньше = ниже пиковая память, но медленнее.")

    st.markdown(f"### Источник: `{st.session_state.get('source_name', 'scene.scad')}`")
    scene = dict_to_scene(payload)
    estimate = _safe_scene_estimate(scene)
    role_counts = _role_summary(scene)

    summary_cols = st.columns(5)
    summary_cols[0].metric("Объекты", len(scene.entities))
    summary_cols[1].metric("Anchors", role_counts.get("anchor", 0))
    summary_cols[2].metric("Loads", role_counts.get("load", 0))
    summary_cols[3].metric("Obstacles", role_counts.get("obstacle", 0))
    summary_cols[4].metric("Voxel size", f"{float(cfg['voxel_size']):.2f}")

    heavy_scene, heavy_reason, suggested_voxel = _estimate_over_limit(cfg, estimate)

    if estimate is not None:
        est_cols = st.columns(4)
        est_cols[0].metric("Активные воксели", _human_int(int(estimate["active_voxels"])))
        est_cols[1].metric("Активные узлы", _human_int(int(estimate["active_nodes"])))
        est_cols[2].metric("DOFs", _human_int(int(estimate["ndofs"])))
        est_cols[3].metric("Оценка матрицы", f"{float(estimate['estimated_matrix_gb']):.1f} GiB")
        if heavy_scene:
            st.warning(
                "Сцена очень тяжёлая. "
                f"{heavy_reason} Рекомендуемый voxel size: от {suggested_voxel:.2f}."
            )
            st.caption("Запуск всё равно можно разрешить вручную. Для очень больших сцен лучше включать matrix-free: он не делает out-of-core LU, но сильно снижает RAM, потому что не собирает/не факторизует глобальную матрицу целиком.")
        else:
            st.success("Оценка FEM укладывается в текущие мягкие лимиты.")

    tabs = st.tabs(["Сцена", "Объекты", "Запуск и результаты", "SCAD"])
    structured_path = work_dir / "generated_scene.scad"
    export_structured_scene(structured_path, scene)

    with tabs[0]:
        voxel_mode = st.checkbox("Показывать в воксельном виде", value=False)
        left, right = st.columns([1.35, 0.65])
        with left:
            st.plotly_chart(build_geometry_figure(scene, voxel_mode), width="stretch", config={"displaylogo": False})
        with right:
            st.markdown("### Что проверить перед запуском")
            st.markdown("- есть хотя бы один anchor и один load;\n- у anchor выставлены фиксации;\n- у load задана ненулевая сила;\n- bbox и voxel size не дают слишком большой FEM.")
            st.markdown("### Роли")
            for role in ROLE_OPTIONS:
                st.markdown(f"<span class='role-chip'>{role}: {role_counts.get(role, 0)}</span>", unsafe_allow_html=True)
            if estimate is not None:
                st.markdown("### Оценка расчёта")
                st.caption(
                    f"Активных вокселей: {_human_int(int(estimate['active_voxels']))}; "
                    f"DOFs: {_human_int(int(estimate['ndofs']))}; "
                    f"матрица: {float(estimate['estimated_matrix_gb']):.1f} GiB."
                )

    with tabs[1]:
        labels = [f"{idx + 1}. {entity['name']} · {entity['role']} · {entity['primitive']['kind']}" for idx, entity in enumerate(payload["entities"])]
        selected_label = st.selectbox("Редактируемый объект", labels)
        idx = labels.index(selected_label)
        entity = payload["entities"][idx]
        apply_role_defaults(entity)

        form_cols = st.columns([1.0, 1.0])
        with form_cols[0]:
            entity["name"] = st.text_input("Имя", value=entity["name"], key=f"name_{idx}")
            role_index = ROLE_OPTIONS.index(entity["role"]) if entity["role"] in ROLE_OPTIONS else ROLE_OPTIONS.index("part")
            entity["role"] = st.selectbox("Роль", ROLE_OPTIONS, index=role_index, key=f"role_{idx}")
            apply_role_defaults(entity)
            mat_index = MATERIAL_OPTIONS.index(entity["material"]) if entity["material"] in MATERIAL_OPTIONS else 0
            entity["material"] = st.selectbox("Материал", MATERIAL_OPTIONS, index=mat_index, key=f"mat_{idx}")
            st.caption(f"Смещение primitive: {entity['primitive']['translate']}")

        with form_cols[1]:
            st.markdown("**Закрепления**")
            fx_cols = st.columns(3)
            entity["fix"][0] = 1 if fx_cols[0].checkbox("X", value=bool(entity["fix"][0]), key=f"fixx_{idx}") else 0
            entity["fix"][1] = 1 if fx_cols[1].checkbox("Y", value=bool(entity["fix"][1]), key=f"fixy_{idx}") else 0
            entity["fix"][2] = 1 if fx_cols[2].checkbox("Z", value=bool(entity["fix"][2]), key=f"fixz_{idx}") else 0
            st.markdown("**Нагрузка, N**")
            f_cols = st.columns(3)
            entity["force"][0] = float(f_cols[0].number_input("Fx", value=float(entity["force"][0]), step=100.0, key=f"forcex_{idx}"))
            entity["force"][1] = float(f_cols[1].number_input("Fy", value=float(entity["force"][1]), step=100.0, key=f"forcey_{idx}"))
            entity["force"][2] = float(f_cols[2].number_input("Fz", value=float(entity["force"][2]), step=100.0, key=f"forcez_{idx}"))

        flags = st.columns(4)
        entity["connect"] = flags[0].checkbox("Учитывать для соединения", value=bool(entity["connect"]), key=f"connect_{idx}")
        entity["structural"] = flags[1].checkbox("Структурный", value=bool(entity["structural"]), key=f"struct_{idx}")
        entity["preserve"] = flags[2].checkbox("Сохранять", value=bool(entity["preserve"]), key=f"preserve_{idx}")
        entity["avoid"] = flags[3].checkbox("Исключать", value=bool(entity["avoid"]), key=f"avoid_{idx}")

        st.info("Теперь редактируется один объект за раз — так проще не запутаться в ролях, нагрузках и ограничениях.")

    payload = st.session_state.scene_payload = payload
    scene = dict_to_scene(payload)
    export_structured_scene(structured_path, scene)

    with tabs[2]:
        st.markdown("### Запуск")
        run_col_1, run_col_2 = st.columns([0.9, 1.1])
        run_col_1.download_button("Скачать структурированный SCAD", data=structured_path.read_bytes(), file_name="generated_scene.scad", width="stretch")
        allow_heavy_run = run_col_2.checkbox("Я понимаю риск и всё равно хочу запуск", value=bool(cfg.get("force_allow_large_problems", True)), help="Это больше не является жёсткой блокировкой. Галка просто явно подтверждает, что ты осознанно запускаешь тяжёлую сцену.")
        cfg["force_allow_large_problems"] = bool(allow_heavy_run)
        if heavy_scene and not allow_heavy_run:
            run_col_2.warning(f"Сцена тяжёлая, но запуск всё равно разрешён. Более безопасный voxel size для старта: от {suggested_voxel:.2f}. Для больших сцен лучше solver=matrix_free или auto с включённым matrix-free.")
        elif heavy_scene and allow_heavy_run:
            run_col_2.warning("Подтверждение принято: запуск пойдёт даже выше мягких лимитов. Для очень больших сцен чаще всего лучше solver=matrix_free или auto с включённым matrix-free, а не direct.")

        if run_col_2.button("Запустить оптимизацию", width="stretch", type="primary"):
            run_root = work_dir / "output"
            run_root.mkdir(parents=True, exist_ok=True)
            result_holder: dict[str, Any] = {}

            def worker() -> None:
                try:
                    result_holder["run_dir"] = run_pipeline(structured_path, output_root=run_root)
                except Exception:
                    result_holder["error"] = traceback.format_exc()

            thread = threading.Thread(target=worker, daemon=True)
            thread.start()
            while True:
                run_dirs = sorted(run_root.glob("run_*"))
                if run_dirs or not thread.is_alive():
                    break
                time.sleep(0.2)
            if not run_dirs:
                st.error("Не удалось создать каталог запуска.")
                if result_holder.get("error"):
                    st.code(result_holder["error"], language="text")
                st.stop()
            run_dir = sorted(run_root.glob("run_*"))[-1]
            st.session_state.last_run_dir = str(run_dir)
            render_monitor(run_dir, thread=thread, result_holder=result_holder)
            st.stop()

        if st.session_state.get("last_run_dir"):
            previous = Path(st.session_state["last_run_dir"])
            if previous.exists():
                st.markdown("---")
                st.markdown("### Последний запуск")
                render_monitor(previous, thread=None)

    with tabs[3]:
        st.markdown("### Сгенерированный структурированный `.scad`")
        st.code(structured_path.read_text(encoding="utf-8"), language="scad")
        st.caption("Итоговый `final_connector.scad` теперь экспортируется как difference: из найденного коннектора вычитаются все компоненты исходного файла.")


if __name__ == "__main__":
    main()
