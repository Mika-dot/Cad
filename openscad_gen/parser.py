
from __future__ import annotations

import ast
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .models import Entity, Primitive, Scene, SceneConfig

ASSIGN_RE = re.compile(r"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.+?)\s*;\s*$")
SCENE_BLOCK_RE = re.compile(r"/\*GD_SCENE(.*?)\*/", re.DOTALL)
TRANSLATE_RE = re.compile(r"translate\s*\((\[.*?\])\)\s*(cube|sphere|cylinder)\s*\((.*)\)\s*;\s*$", re.DOTALL)
PLAIN_TRANSLATE_RE = re.compile(r"translate\s*\((\[.*?\])\)\s*(cube|sphere|cylinder)\s*\((.*?)\)\s*;", re.DOTALL)


@dataclass
class ModuleDef:
    name: str
    params_text: str
    body: str


class SafeEval(ast.NodeVisitor):
    def __init__(self, variables: dict[str, float]):
        self.variables = variables

    def visit_Expression(self, node: ast.Expression) -> Any:
        return self.visit(node.body)

    def visit_Name(self, node: ast.Name) -> Any:
        if node.id not in self.variables:
            raise ValueError(f"Unknown variable: {node.id}")
        return self.variables[node.id]

    def visit_Constant(self, node: ast.Constant) -> Any:
        if isinstance(node.value, (int, float, bool)):
            return node.value
        raise ValueError(f"Unsupported constant: {node.value!r}")

    def visit_Num(self, node: ast.Num) -> Any:  # pragma: no cover
        return node.n

    def visit_UnaryOp(self, node: ast.UnaryOp) -> Any:
        operand = self.visit(node.operand)
        if isinstance(node.op, ast.UAdd):
            return +operand
        if isinstance(node.op, ast.USub):
            return -operand
        raise ValueError("Unsupported unary operator")

    def visit_BinOp(self, node: ast.BinOp) -> Any:
        left = self.visit(node.left)
        right = self.visit(node.right)
        if isinstance(node.op, ast.Add):
            return left + right
        if isinstance(node.op, ast.Sub):
            return left - right
        if isinstance(node.op, ast.Mult):
            return left * right
        if isinstance(node.op, ast.Div):
            return left / right
        if isinstance(node.op, ast.Pow):
            return left ** right
        raise ValueError("Unsupported binary operator")

    def generic_visit(self, node: ast.AST) -> Any:
        raise ValueError(f"Unsupported expression: {ast.dump(node)}")


def safe_eval(expr: str, variables: dict[str, float]) -> Any:
    expr = expr.strip()
    tree = ast.parse(expr, mode="eval")
    return SafeEval(variables).visit(tree)


def split_args(arg_text: str) -> list[str]:
    parts: list[str] = []
    current: list[str] = []
    depth = 0
    in_string = False
    string_char = ""
    for ch in arg_text:
        if in_string:
            current.append(ch)
            if ch == string_char:
                in_string = False
            continue
        if ch in ('"', "'"):
            in_string = True
            string_char = ch
            current.append(ch)
            continue
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        if ch == "," and depth == 0:
            part = "".join(current).strip()
            if part:
                parts.append(part)
            current = []
        else:
            current.append(ch)
    tail = "".join(current).strip()
    if tail:
        parts.append(tail)
    return parts


def parse_value(text: str, variables: dict[str, float]) -> Any:
    raw = text.strip()
    low = raw.lower()
    if low == "true":
        return True
    if low == "false":
        return False
    if low == "none":
        return None
    if (raw.startswith('"') and raw.endswith('"')) or (raw.startswith("'") and raw.endswith("'")):
        return ast.literal_eval(raw)
    if raw.startswith("[") and raw.endswith("]"):
        inner = raw[1:-1].strip()
        if not inner:
            return []
        return [parse_value(part, variables) for part in split_args(inner)]
    try:
        return safe_eval(raw, variables)
    except Exception:
        return raw


def parse_block(block_text: str, variables: dict[str, float]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for line in block_text.strip().splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        if ":" not in stripped:
            continue
        key, value = stripped.split(":", 1)
        result[key.strip()] = parse_value(value.strip(), variables)
    return result


def parse_primitive(statement: str, variables: dict[str, float]) -> Primitive:
    match = TRANSLATE_RE.match(statement.strip())
    if not match:
        raise ValueError(f"Unsupported entity statement: {statement.strip()!r}")

    translate_text, kind, arg_text = match.groups()
    translate = parse_value(translate_text, variables)
    if not isinstance(translate, list) or len(translate) != 3:
        raise ValueError("translate([...]) must have exactly 3 values")

    raw_parts = split_args(arg_text)
    params: dict[str, Any] = {}
    positional: list[Any] = []
    for part in raw_parts:
        if "=" in part:
            key, value = part.split("=", 1)
            params[key.strip()] = parse_value(value.strip(), variables)
        else:
            positional.append(parse_value(part, variables))

    if kind == "cube":
        if positional:
            params.setdefault("size", positional[0])
        params.setdefault("center", False)
    elif kind == "sphere":
        if positional:
            params.setdefault("r", positional[0])
    elif kind == "cylinder":
        if positional:
            if len(positional) >= 1:
                params.setdefault("h", positional[0])
            if len(positional) >= 2:
                params.setdefault("r", positional[1])
        params.setdefault("center", False)

    return Primitive(
        kind=kind,
        params=params,
        translate=(float(translate[0]), float(translate[1]), float(translate[2])),
    )


def parse_variables(text: str) -> dict[str, float]:
    variables: dict[str, float] = {}
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("//"):
            continue
        if stripped.startswith("/*") or stripped.startswith("*/"):
            continue
        match = ASSIGN_RE.match(line)
        if not match:
            continue
        name, expr = match.groups()
        variables[name] = float(safe_eval(expr, variables))
    return variables


def skip_ws(text: str, idx: int) -> int:
    n = len(text)
    while idx < n:
        if text.startswith("//", idx):
            idx = text.find("\n", idx)
            if idx == -1:
                return n
            continue
        if text.startswith("/*", idx):
            end = text.find("*/", idx + 2)
            if end == -1:
                return n
            idx = end + 2
            continue
        if text[idx].isspace():
            idx += 1
            continue
        break
    return idx


def find_matching(text: str, idx: int, open_ch: str, close_ch: str) -> int:
    assert text[idx] == open_ch
    depth = 0
    in_string = False
    string_char = ""
    i = idx
    while i < len(text):
        ch = text[i]
        if in_string:
            if ch == string_char and text[i - 1] != "\\":
                in_string = False
            i += 1
            continue
        if ch in ('"', "'"):
            in_string = True
            string_char = ch
            i += 1
            continue
        if text.startswith("//", i):
            nxt = text.find("\n", i)
            if nxt == -1:
                return len(text) - 1
            i = nxt + 1
            continue
        if text.startswith("/*", i):
            nxt = text.find("*/", i + 2)
            if nxt == -1:
                raise ValueError("Unterminated block comment")
            i = nxt + 2
            continue
        if ch == open_ch:
            depth += 1
        elif ch == close_ch:
            depth -= 1
            if depth == 0:
                return i
        i += 1
    raise ValueError(f"Unmatched {open_ch}")


def _extract_next_statement(text: str, idx: int) -> tuple[str, int]:
    idx = skip_ws(text, idx)
    start = idx
    while idx < len(text):
        if text[idx] == "{":
            idx = find_matching(text, idx, "{", "}") + 1
            probe = skip_ws(text, idx)
            if probe >= len(text) or text.startswith("/*GD_ENTITY", probe) or text[probe] != ";":
                return text[start:idx].strip(), idx
            continue
        if text[idx] == "(":
            idx = find_matching(text, idx, "(", ")") + 1
            continue
        if text[idx] == ";":
            return text[start: idx + 1], idx + 1
        idx += 1
    return text[start:].strip(), len(text)


def extract_gd_entities(text: str) -> list[tuple[str, str]]:
    items: list[tuple[str, str]] = []
    idx = 0
    tag = "/*GD_ENTITY"
    while True:
        start = text.find(tag, idx)
        if start == -1:
            break
        block_end = text.find("*/", start)
        if block_end == -1:
            raise ValueError("Unterminated GD_ENTITY block")
        block_text = text[start + len(tag):block_end]
        body, next_idx = _extract_next_statement(text, block_end + 2)
        body = body.strip()
        if body:
            items.append((block_text, body))
        idx = next_idx
    return items


def extract_module_definitions(text: str) -> dict[str, ModuleDef]:
    modules: dict[str, ModuleDef] = {}
    idx = 0
    tag = "module"
    while True:
        m = re.search(r"\bmodule\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(", text[idx:])
        if not m:
            break
        abs_start = idx + m.start()
        name = m.group(1)
        paren_start = text.find("(", abs_start)
        paren_end = find_matching(text, paren_start, "(", ")")
        body_start = skip_ws(text, paren_end + 1)
        if body_start >= len(text) or text[body_start] != "{":
            idx = paren_end + 1
            continue
        body_end = find_matching(text, body_start, "{", "}")
        params_text = text[paren_start + 1:paren_end]
        body = text[body_start + 1:body_end]
        modules[name] = ModuleDef(name=name, params_text=params_text, body=body)
        idx = body_end + 1
    return modules


def parse_call_arguments(arg_text: str, variables: dict[str, Any]) -> tuple[list[Any], dict[str, Any]]:
    positional: list[Any] = []
    kwargs: dict[str, Any] = {}
    for part in split_args(arg_text):
        if not part:
            continue
        if "=" in part:
            key, value = part.split("=", 1)
            kwargs[key.strip()] = parse_value(value.strip(), variables)
        else:
            positional.append(parse_value(part, variables))
    return positional, kwargs


def bind_module_variables(module: ModuleDef, call_arg_text: str, parent_vars: dict[str, Any]) -> dict[str, Any]:
    local_vars = dict(parent_vars)
    formals = [p.strip() for p in split_args(module.params_text) if p.strip()]
    actual_positional, actual_kwargs = parse_call_arguments(call_arg_text, parent_vars)
    pos_idx = 0
    for formal in formals:
        if "=" in formal:
            name, default = formal.split("=", 1)
            name = name.strip()
            default_value = parse_value(default.strip(), local_vars)
        else:
            name = formal.strip()
            default_value = None
        if name in actual_kwargs:
            local_vars[name] = actual_kwargs[name]
        elif pos_idx < len(actual_positional):
            local_vars[name] = actual_positional[pos_idx]
            pos_idx += 1
        else:
            local_vars[name] = default_value
    return local_vars


def parse_tree_body(body: str, variables: dict[str, Any], modules: dict[str, ModuleDef]) -> dict[str, Any]:
    children: list[dict[str, Any]] = []
    idx = 0
    n = len(body)
    while True:
        idx = skip_ws(body, idx)
        if idx >= n:
            break
        node, idx = parse_tree_node(body, idx, variables, modules)
        if node is not None:
            children.append(node)
        idx = skip_ws(body, idx)
        if idx < n and body[idx] == ";":
            idx += 1
    if not children:
        return {"type": "empty"}
    if len(children) == 1:
        return children[0]
    return {"type": "union", "children": children}


def parse_tree_node(text: str, idx: int, variables: dict[str, Any], modules: dict[str, ModuleDef]) -> tuple[dict[str, Any] | None, int]:
    idx = skip_ws(text, idx)
    n = len(text)
    if idx >= n:
        return None, idx
    if text[idx] == "{":
        end = find_matching(text, idx, "{", "}")
        node = parse_tree_body(text[idx + 1:end], variables, modules)
        return node, end + 1

    m = re.match(r"([A-Za-z_][A-Za-z0-9_]*)", text[idx:])
    if not m:
        raise ValueError(f"Cannot parse OpenSCAD near: {text[idx:idx+80]!r}")
    name = m.group(1)
    idx += len(name)
    idx = skip_ws(text, idx)

    arg_text = ""
    if idx < n and text[idx] == "(":
        end = find_matching(text, idx, "(", ")")
        arg_text = text[idx + 1:end]
        idx = end + 1
    idx = skip_ws(text, idx)

    if name in {"translate", "rotate", "color", "union", "difference", "place_entity"}:
        if idx < n and text[idx] == "{":
            end = find_matching(text, idx, "{", "}")
            children_node = parse_tree_body(text[idx + 1:end], variables, modules)
            idx = end + 1
        else:
            children_node, idx = parse_tree_node(text, idx, variables, modules)
            if children_node is None:
                children_node = {"type": "empty"}
        positional, kwargs = parse_call_arguments(arg_text, variables)
        if name == "translate":
            vec = positional[0] if positional else kwargs.get("v", [0, 0, 0])
            node = {"type": "translate", "vec": [float(v) for v in vec], "child": children_node}
        elif name == "rotate":
            angles = positional[0] if positional else kwargs.get("a", [0, 0, 0])
            node = {"type": "rotate", "angles": [float(v) for v in angles], "child": children_node}
        elif name == "color":
            node = children_node
        elif name == "union":
            node = children_node if children_node.get("type") == "union" else {"type": "union", "children": [children_node]}
        elif name == "difference":
            if children_node.get("type") == "union":
                ch = children_node.get("children", [])
            else:
                ch = [children_node]
            if not ch:
                node = {"type": "empty"}
            else:
                node = {"type": "difference", "base": ch[0], "subtract": ch[1:]}
        else:  # place_entity
            pos = positional[0] if positional else kwargs.get("pos", [0, 0, 0])
            rot = positional[1] if len(positional) > 1 else kwargs.get("rot", [0, 0, 0])
            node = {
                "type": "translate",
                "vec": [float(v) for v in pos],
                "child": {"type": "rotate", "angles": [float(v) for v in rot], "child": children_node},
            }
        idx = skip_ws(text, idx)
        if idx < n and text[idx] == ";":
            idx += 1
        return node, idx

    if name in {"cube", "sphere", "cylinder"}:
        positional, kwargs = parse_call_arguments(arg_text, variables)
        params = dict(kwargs)
        if name == "cube":
            if positional:
                params.setdefault("size", positional[0])
            params.setdefault("center", False)
        elif name == "sphere":
            if positional:
                params.setdefault("r", positional[0])
        elif name == "cylinder":
            if positional:
                if len(positional) >= 1:
                    params.setdefault("h", positional[0])
                if len(positional) >= 2:
                    params.setdefault("r", positional[1])
            if "d" in params and "r" not in params:
                params["r"] = float(params["d"]) / 2.0
            if "d1" in params and "r1" not in params:
                params["r1"] = float(params["d1"]) / 2.0
            if "d2" in params and "r2" not in params:
                params["r2"] = float(params["d2"]) / 2.0
            params.setdefault("center", False)
        node = {"type": name, "params": params}
        idx = skip_ws(text, idx)
        if idx < n and text[idx] == ";":
            idx += 1
        return node, idx

    if name == "children":
        idx = skip_ws(text, idx)
        if idx < n and text[idx] == ";":
            idx += 1
        return {"type": "empty"}, idx

    if name in modules:
        local_vars = bind_module_variables(modules[name], arg_text, variables)
        node = parse_tree_body(modules[name].body, local_vars, modules)
        idx = skip_ws(text, idx)
        if idx < n and text[idx] == ";":
            idx += 1
        return node, idx

    raise ValueError(f"Unsupported OpenSCAD construct: {name}")


def infer_translate_from_tree(node: dict[str, Any]) -> tuple[float, float, float]:
    if node.get("type") == "translate":
        vec = node.get("vec", [0.0, 0.0, 0.0])
        return float(vec[0]), float(vec[1]), float(vec[2])
    return 0.0, 0.0, 0.0


def parse_entity_statement(statement: str, variables: dict[str, Any], modules: dict[str, ModuleDef]) -> Primitive:
    try:
        return parse_primitive(statement, variables)
    except Exception:
        tree = parse_tree_body(statement.strip(), variables, modules)
        return Primitive(kind="compound", params={"tree": tree, "source": statement.strip()}, translate=infer_translate_from_tree(tree))


def parse_scene(path: str | Path) -> Scene:
    scene_path = Path(path)
    text = scene_path.read_text(encoding="utf-8")
    variables = parse_variables(text)
    modules = extract_module_definitions(text)

    config = SceneConfig()
    scene_match = SCENE_BLOCK_RE.search(text)
    if scene_match:
        cfg_data = parse_block(scene_match.group(1), variables)
        for key, value in cfg_data.items():
            if hasattr(config, key):
                setattr(config, key, value)

    entities: list[Entity] = []
    for block_text, statement in extract_gd_entities(text):
        data = parse_block(block_text, variables)
        primitive = parse_entity_statement(statement, variables, modules)
        entities.append(
            Entity(
                name=str(data.get("name", f"entity_{len(entities)}")),
                role=str(data.get("role", "part")),
                material=str(data.get("material", config.connector_material)),
                primitive=primitive,
                fix=tuple(int(v) for v in data.get("fix", [0, 0, 0])),
                force=tuple(float(v) for v in data.get("force", [0.0, 0.0, 0.0])),
                connect=bool(data.get("connect", False)),
                structural=bool(data.get("structural", True)),
                preserve=bool(data.get("preserve", False)),
                avoid=bool(data.get("avoid", False)),
            )
        )

    if not entities:
        raise ValueError("No GD_ENTITY blocks were found in the scene file.")

    return Scene(config=config, variables=variables, entities=entities, path=scene_path)


def parse_plain_scad(path: str | Path) -> Scene:
    scene_path = Path(path)
    text = scene_path.read_text(encoding="utf-8")
    variables = parse_variables(text)
    config = SceneConfig()

    entities: list[Entity] = []
    for index, match in enumerate(PLAIN_TRANSLATE_RE.finditer(text), start=1):
        statement = match.group(0)
        primitive = parse_primitive(statement, variables)
        role = "part"
        fix = (0, 0, 0)
        force = (0.0, 0.0, 0.0)
        connect = True if index in (1, 3) else False
        structural = True
        preserve = True if index in (1, 3) else False
        avoid = False
        if index == 1:
            role = "anchor"
            fix = (1, 1, 1)
        elif index == 2:
            role = "obstacle"
            connect = False
            structural = False
            preserve = False
            avoid = True
        elif index == 3:
            role = "load"
            force = (0.0, 0.0, -1000.0)
        entities.append(
            Entity(
                name=f"obj_{index}_{primitive.kind}",
                role=role,
                material=config.connector_material,
                primitive=primitive,
                fix=fix,
                force=force,
                connect=connect,
                structural=structural,
                preserve=preserve,
                avoid=avoid,
            )
        )

    if not entities:
        raise ValueError("No translate(...) primitives found in plain .scad file.")
    return Scene(config=config, variables=variables, entities=entities, path=scene_path)


def parse_auto_scene(path: str | Path) -> Scene:
    text = Path(path).read_text(encoding="utf-8")
    if "/*GD_ENTITY" in text:
        return parse_scene(path)
    return parse_plain_scad(path)
