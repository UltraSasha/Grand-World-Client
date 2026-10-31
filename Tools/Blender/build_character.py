# Генератор персонажа Grand World для Blender 5.x.
# Строит Humanoid-скелет, стилизованный меш (повседневная одежда), анимации и экспортирует FBX.
# Запуск в Blender: exec(open(r"C:\Grand World RP\Client\Tools\Blender\build_character.py", encoding="utf-8").read())
# Работает в отдельной сцене GW_Character, остальные сцены файла не трогает.
import bpy, bmesh, math, os
from mathutils import Vector, Quaternion, Matrix

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))) if "__file__" in globals() else r"C:\Grand World RP\Client"
EXPORT_PATH = os.path.join(ROOT, "Grand World", "Assets", "_Game", "Art", "Characters", "Player", "Player.fbx")
FPS = 30

# ---------------------------------------------------------------- сцена
scn = bpy.data.scenes.get("GW_Character") or bpy.data.scenes.new("GW_Character")
bpy.context.window.scene = scn
scn.render.fps = FPS
for o in list(scn.collection.all_objects):
    bpy.data.objects.remove(o, do_unlink=True)
for a in list(bpy.data.actions):
    if a.name.startswith("GW_"):
        bpy.data.actions.remove(a)

# ---------------------------------------------------------------- скелет
arm_data = bpy.data.armatures.new("GW_Armature")
arm = bpy.data.objects.new("Armature", arm_data)
scn.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones
FWD = Vector((0, -1, 0)); UP = Vector((0, 0, 1))

def add_bone(name, h, t, parent=None, connect=False, rollvec=FWD):
    b = eb.new(name); b.head = Vector(h); b.tail = Vector(t)
    if parent:
        b.parent = eb[parent]; b.use_connect = connect
    b.align_roll(rollvec)
    return b

add_bone("Hips", (0, 0, 0.95), (0, 0, 1.06))
add_bone("Spine", (0, 0, 1.06), (0, 0, 1.22), "Hips", True)
add_bone("Chest", (0, 0, 1.22), (0, 0, 1.48), "Spine", True)
add_bone("Neck", (0, 0, 1.48), (0, 0, 1.58), "Chest", True)
add_bone("Head", (0, 0, 1.58), (0, 0, 1.80), "Neck", True)
for side, s in (("Left", 1), ("Right", -1)):
    add_bone(side + "Shoulder", (0.03 * s, 0, 1.44), (0.17 * s, 0, 1.44), "Chest", False, UP)
    add_bone(side + "UpperArm", (0.17 * s, 0, 1.44), (0.45 * s, 0, 1.44), side + "Shoulder", True, UP)
    add_bone(side + "LowerArm", (0.45 * s, 0, 1.44), (0.70 * s, 0, 1.44), side + "UpperArm", True, UP)
    add_bone(side + "Hand", (0.70 * s, 0, 1.44), (0.86 * s, 0, 1.44), side + "LowerArm", True, UP)
    add_bone(side + "UpperLeg", (0.10 * s, 0, 0.94), (0.10 * s, 0, 0.52), "Hips")
    add_bone(side + "LowerLeg", (0.10 * s, 0, 0.52), (0.10 * s, 0, 0.10), side + "UpperLeg", True)
    add_bone(side + "Foot", (0.10 * s, 0, 0.10), (0.10 * s, -0.13, 0.035), side + "LowerLeg", True, UP)
    add_bone(side + "Toes", (0.10 * s, -0.13, 0.035), (0.10 * s, -0.22, 0.035), side + "Foot", True, UP)
bpy.ops.object.mode_set(mode='OBJECT')
BONE_NAMES = [b.name for b in arm_data.bones]

# ---------------------------------------------------------------- материалы
def make_mat(name, col, rough=0.6, metal=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    p.inputs["Base Color"].default_value = (*col, 1)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    m.diffuse_color = (*col, 1)
    return m

MATS = {
    "Skin":   make_mat("GW_Skin",   (0.80, 0.58, 0.45), 0.55),
    "Hair":   make_mat("GW_Hair",   (0.10, 0.07, 0.05), 0.70),
    "Eyes":   make_mat("GW_Eyes",   (0.03, 0.03, 0.035), 0.20),
    "Hoodie": make_mat("GW_Hoodie", (0.20, 0.24, 0.17), 0.85),
    "Shirt":  make_mat("GW_Shirt",  (0.78, 0.76, 0.70), 0.85),
    "Jeans":  make_mat("GW_Jeans",  (0.06, 0.09, 0.17), 0.80),
    "Shoes":  make_mat("GW_Shoes",  (0.88, 0.88, 0.86), 0.60),
    "Sole":   make_mat("GW_Sole",   (0.12, 0.12, 0.13), 0.80),
}
MAT_INDEX = {k: i for i, k in enumerate(MATS)}

# ---------------------------------------------------------------- меш
bm = bmesh.new()
DEFORM = bm.verts.layers.deform.verify()

def _smooth(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a))) if b != a else 1.0
    return t * t * (3 - 2 * t)

def _radius(prof, y):
    if y <= prof[0][0]:
        return prof[0][1]
    for (y0, r0), (y1, r1) in zip(prof, prof[1:]):
        if y <= y1:
            return r0 + (r1 - r0) * _smooth(y0, y1, y)
    return prof[-1][1]

def piece(bone, prof, sx=1.0, sz=1.0, mat="Skin", offset=(0, 0, 0), rot=0.0, cap0=None, cap1=None,
          seg=24, rings=26, flat_bottom=None, frame=None, bone_fn=None):
    """Тело вращения вдоль оси Y кости.
    prof: [(y, радиус)], sx/sz: сплющивание по X/Z кости, offset: смещение в осях кости,
    rot: наклон вокруг X кости (градусы), cap0/cap1: длина скруглений на концах,
    flat_bottom: срезать всё ниже этой локальной Z (подошва),
    frame: своя система координат вместо кости, bone_fn(y) -> имя кости для вершины (иначе bone)."""
    B = frame if frame is not None else arm.data.bones[bone].matrix_local
    ya, yb = prof[0][0], prof[-1][0]
    c0 = prof[0][1] if cap0 is None else cap0
    c1 = prof[-1][1] if cap1 is None else cap1
    y0, y1 = ya - c0, yb + c1
    ymid = (ya + yb) / 2
    R = Matrix.Rotation(math.radians(rot), 4, 'X')
    gi = BONE_NAMES.index(bone)
    verts = []
    for i in range(rings + 1):
        u = i / rings
        y = y0 + (y1 - y0) * (0.5 - 0.5 * math.cos(math.pi * u))
        r = _radius(prof, min(max(y, ya), yb))
        if y < ya and c0 > 0: r *= math.sqrt(max(0.0, 1 - ((ya - y) / c0) ** 2))
        if y > yb and c1 > 0: r *= math.sqrt(max(0.0, 1 - ((y - yb) / c1) ** 2))
        ring = []
        for j in range(seg):
            a = 2 * math.pi * j / seg
            lx, lz = math.cos(a) * r * sx, math.sin(a) * r * sz
            if flat_bottom is not None:
                lz = max(lz, flat_bottom)
            p = R @ Vector((lx, y - ymid, lz)) + Vector((0, ymid, 0)) + Vector(offset)
            v = bm.verts.new(B @ p)
            v[DEFORM][BONE_NAMES.index(bone_fn(y)) if bone_fn else gi] = 1.0
            ring.append(v)
        verts.append(ring)
    for k in range(len(verts) - 1):
        for j in range(seg):
            f = bm.faces.new((verts[k][j], verts[k + 1][j], verts[k + 1][(j + 1) % seg], verts[k][(j + 1) % seg]))
            f.material_index = MAT_INDEX[mat]
            f.smooth = True

def blob(bone, center, radii, mat, rot=0.0, seg=16, rings=12):
    """Эллипсоид в осях кости: center (x, y, z), radii (rx, ry, rz)."""
    piece(bone, [(center[1], 1.0), (center[1], 1.0)], sx=radii[0], sz=radii[2], mat=mat,
          offset=(center[0], 0, center[2]), rot=rot, cap0=radii[1], cap1=radii[1], seg=seg, rings=rings)

# --- торс и голова (у позвоночника локальная Z смотрит вперёд, X — вбок)
piece("Hips",  [(-0.06, 0.150), (0.04, 0.162), (0.12, 0.152)], 1.0, 0.72, "Jeans")
piece("Spine", [(0.0, 0.142), (0.10, 0.140), (0.18, 0.150)], 1.03, 0.72, "Hoodie")
piece("Chest", [(0.02, 0.150), (0.14, 0.172), (0.22, 0.170), (0.27, 0.120)], 1.12, 0.70, "Hoodie")
piece("Hips",  [(0.08, 0.158), (0.13, 0.156)], 1.02, 0.74, "Hoodie", cap0=0.01, cap1=0.01, seg=32, rings=6)  # низ худи
blob("Chest", (0, 0.26, -0.075), (0.115, 0.055, 0.045), "Hoodie", rot=-15)    # капюшон на спине
piece("Neck",  [(0.0, 0.050), (0.10, 0.046)], 1.0, 1.0, "Skin")
piece("Chest", [(0.25, 0.075), (0.28, 0.068)], 1.0, 0.95, "Hoodie", cap0=0.02, cap1=0.01, seg=24, rings=6)  # ворот
piece("Head",  [(0.03, 0.070), (0.08, 0.090), (0.14, 0.100), (0.18, 0.094)], 0.86, 1.0, "Skin", cap1=0.065)
blob("Head", (0, 0.108, 0.094), (0.011, 0.017, 0.012), "Skin", rot=-10)       # нос
for sx_ in (1, -1):
    blob("Head", (0.035 * sx_, 0.128, 0.090), (0.012, 0.010, 0.006), "Eyes")  # глаза
    blob("Head", (0.036 * sx_, 0.146, 0.092), (0.018, 0.004, 0.005), "Hair")  # брови
    blob("Head", (0.086 * sx_, 0.115, 0.002), (0.010, 0.022, 0.016), "Skin")  # уши
piece("Head", [(0.12, 0.107), (0.18, 0.103)], 0.93, 1.08, "Hair", offset=(0, 0.016, -0.016), rot=-20,
      cap0=0.05, cap1=0.088)                                                    # причёска

# --- руки (у рук локальная Z смотрит вверх, X — вперёд у левой и назад у правой)
for side, s in (("Left", 1), ("Right", -1)):
    blob(side + "UpperArm", (0, 0.005, 0.005), (0.068, 0.07, 0.066), "Hoodie")  # плечевой сустав
    piece(side + "UpperArm", [(0.02, 0.060), (0.14, 0.056), (0.27, 0.050)], 1.0, 1.0, "Hoodie")
    piece(side + "LowerArm", [(0.0, 0.050), (0.10, 0.050), (0.20, 0.044)], 1.0, 1.0, "Hoodie")
    piece(side + "LowerArm", [(0.20, 0.047), (0.235, 0.045)], 1.0, 1.0, "Hoodie", cap0=0.008, cap1=0.008, rings=6)  # манжета
    piece(side + "Hand", [(0.01, 0.036), (0.07, 0.050), (0.12, 0.046)], 1.0, 0.56, "Skin", cap1=0.035)
    blob(side + "Hand", (0.044 * s, 0.040, -0.006), (0.016, 0.034, 0.016), "Skin", rot=0)  # большой палец

# --- ноги (у ног локальная Z смотрит вперёд)
for side, s in (("Left", 1), ("Right", -1)):
    piece(side + "UpperLeg", [(0.0, 0.098), (0.15, 0.090), (0.40, 0.068)], 1.0, 1.0, "Jeans")
    piece(side + "LowerLeg", [(0.02, 0.066), (0.14, 0.064), (0.36, 0.058)], 1.0, 1.0, "Jeans")
    piece(side + "LowerLeg", [(0.33, 0.061), (0.37, 0.060)], 1.0, 1.0, "Jeans", cap0=0.01, cap1=0.01, rings=6)  # подворот
    # кроссовок строится горизонтально: начало — пятка, Y — вперёд, Z — вверх; носок привязан к Toes
    shoe = Matrix(((-1, 0, 0, 0.10 * s), (0, -1, 0, 0.055), (0, 0, 1, 0.0), (0, 0, 0, 1)))
    toe_fn = (lambda y, side=side: side + ("Toes" if y > 0.19 else "Foot"))
    shoe_prof = [(0.03, 0.048), (0.12, 0.054), (0.20, 0.050), (0.25, 0.040)]
    piece(side + "Foot", shoe_prof, 0.86, 0.95, "Shoes", offset=(0, 0, 0.060), flat_bottom=-0.035,
          frame=shoe, bone_fn=toe_fn, cap0=0.035, cap1=0.03)
    piece(side + "Foot", [(y, r + 0.006) for y, r in shoe_prof], 0.88, 0.30, "Sole", offset=(0, 0, 0.022),
          flat_bottom=-0.022, frame=shoe, bone_fn=toe_fn, cap0=0.04, cap1=0.035)  # подошва

bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
me = bpy.data.meshes.new("GW_Body")
bm.to_mesh(me); bm.free()
for m in MATS.values():
    me.materials.append(m)
body = bpy.data.objects.new("Body", me)
scn.collection.objects.link(body)
for n in BONE_NAMES:
    body.vertex_groups.new(name=n)
body.parent = arm
body.modifiers.new("Armature", "ARMATURE").object = arm

# ---------------------------------------------------------------- позы
REST = {b.name: b.matrix_local.to_quaternion() for b in arm.data.bones}
X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)
THIGH, SHIN = 0.42, 0.42
HIP_Z, ANKLE_Z = 0.94, 0.10

def wq(axis, deg):
    return Quaternion(Vector(axis), math.radians(deg))

def set_world(pb, q):
    """Поворот кости, заданный в осях арматуры относительно её покоя."""
    r = REST[pb.name]
    pb.rotation_mode = 'QUATERNION'
    pb.rotation_quaternion = r.inverted() @ q @ r

def leg_ik(fwd, drop):
    """Двухзвенный IK ноги в сагиттальной плоскости. Возвращает (угол бедра вперёд, сгиб колена), градусы."""
    d = min(max(math.hypot(fwd, drop), 0.05), (THIGH + SHIN) * 0.999)
    knee = math.pi - math.acos(max(-1, min(1, (THIGH ** 2 + SHIN ** 2 - d * d) / (2 * THIGH * SHIN))))
    thigh = math.atan2(fwd, drop) + math.asin(max(-1, min(1, SHIN * math.sin(knee) / d)))
    return math.degrees(thigh), math.degrees(knee)

def pose(P):
    pbs = arm.pose.bones
    for pb in pbs:
        pb.rotation_mode = 'QUATERNION'; pb.rotation_quaternion = (1, 0, 0, 0); pb.location = (0, 0, 0)
    hz, hx, hf = P.get("hip_z", 0.0), P.get("hip_x", 0.0), P.get("hip_f", 0.0)
    hp, hyaw, hroll = P.get("hip_pitch", 0.0), P.get("hip_yaw", 0.0), P.get("hip_roll", 0.0)
    pbs["Hips"].location = REST["Hips"].inverted() @ Vector((hx, -hf, hz))
    set_world(pbs["Hips"], wq(Z, hyaw) @ wq(Y, hroll) @ wq(X, hp))
    set_world(pbs["Spine"], wq(X, P.get("spine", 0)) @ wq(Z, -hyaw * 0.5) @ wq(Y, -hroll * 0.6))
    set_world(pbs["Chest"], wq(X, P.get("chest", 0)) @ wq(Z, P.get("chest_yaw", -hyaw * 0.6)) @ wq(Y, P.get("chest_roll", 0)))
    set_world(pbs["Neck"], wq(X, P.get("neck", 0)))
    set_world(pbs["Head"], wq(X, P.get("head", 0)) @ wq(Z, P.get("head_yaw", 0)) @ wq(Y, P.get("head_roll", 0)))
    for side, s in (("Left", 1), ("Right", -1)):
        k = side[0]
        f_fwd, f_h = P.get(k + "foot", (0.0, 0.0))
        thigh, knee = leg_ik(f_fwd - hf, (HIP_Z + hz) - (ANKLE_Z + f_h))
        set_world(pbs[side + "UpperLeg"], wq(X, -(thigh + hp)) @ wq(Y, -s * P.get("leg_out", 2.0)))
        set_world(pbs[side + "LowerLeg"], wq(X, knee))
        set_world(pbs[side + "Foot"], wq(X, thigh - knee + P.get(k + "foot_pitch", 0.0)))
        set_world(pbs[side + "Toes"], wq(X, P.get(k + "toe", 0.0)))
        set_world(pbs[side + "Shoulder"], wq(Y, s * P.get("shrug", 0.0)))
        set_world(pbs[side + "UpperArm"],
                  wq(X, -P.get(k + "arm", 0.0)) @ wq(Y, s * P.get("arm_down", 74.0)) @ wq(Z, s * P.get("arm_in", 0.0)))
        set_world(pbs[side + "LowerArm"], wq(Z, -s * P.get(k + "elbow", 12.0)))
        set_world(pbs[side + "Hand"], wq(Y, s * P.get("wrist", 4.0)))

def key_frame(f):
    for pb in arm.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=f)
        pb.keyframe_insert("location", frame=f)

if arm.animation_data is None:
    arm.animation_data_create()

def make_clip(name, frames, fn, loop=True):
    """fn(t) -> словарь позы, t в [0, 1]. Для цикла последний кадр совпадает с первым."""
    arm.animation_data.action = None
    for f in range(frames + 1):
        t = f / frames
        pose(fn(t))
        key_frame(f)
    act = arm.animation_data.action
    act.name = "GW_" + name
    act.use_fake_user = True
    act.frame_range = (0, frames)
    for fc in getattr(act, "fcurves", []):
        for kp in fc.keyframe_points:
            kp.interpolation = 'LINEAR' if loop else 'BEZIER'
    return act

def ease(x):
    return x * x * (3 - 2 * x)

def gait(t, stride, lift, stance, base_z, bob, lean, arm_amp, elbow, extra=None):
    """Цикл шага: левая нога в фазе t, правая — со сдвигом 0.5."""
    P = {"hip_z": base_z + bob * math.cos(4 * math.pi * t), "spine": lean * 0.6, "chest": lean * 0.4,
         "head": -lean * 0.6, "hip_yaw": 5.0 * math.cos(2 * math.pi * t) * (stride / 0.5),
         "hip_roll": 2.0 * math.sin(2 * math.pi * t)}
    for k, ph in (("L", t), ("R", (t + 0.5) % 1.0)):
        if ph < stance:
            q = ph / stance
            P[k + "foot"] = (stride / 2 - stride * q, 0.0)
            P[k + "foot_pitch"] = -18.0 * max(0.0, (q - 0.7) / 0.3)          # отрыв пятки в конце опоры
        else:
            q = (ph - stance) / (1 - stance)
            P[k + "foot"] = (-stride / 2 + stride * ease(q), lift * math.sin(math.pi * q))
            P[k + "foot_pitch"] = -18.0 * (1 - q) + 8.0 * math.sin(math.pi * q) * 0.0
    P["Rarm"] = arm_amp * math.cos(2 * math.pi * t)
    P["Larm"] = -arm_amp * math.cos(2 * math.pi * t)
    P["Lelbow"] = elbow + elbow * 0.25 * max(0, P["Larm"]) / max(arm_amp, 1)
    P["Relbow"] = elbow + elbow * 0.25 * max(0, P["Rarm"]) / max(arm_amp, 1)
    if extra:
        P.update(extra)
    return P

# ---------------------------------------------------------------- клипы
CLIPS = []

def idle(t):
    b = math.sin(2 * math.pi * t)
    return {"hip_z": -0.012 + 0.004 * b, "hip_x": 0.012 * math.sin(2 * math.pi * t + 0.5), "spine": 1.5,
            "chest": 1.0 + 1.2 * b, "head": -1.5 - 0.8 * b, "head_yaw": 3 * math.sin(2 * math.pi * t),
            "Lfoot": (0.03, 0), "Rfoot": (-0.04, 0), "leg_out": 4.0, "arm_down": 76 - 1.0 * b,
            "Larm": 2, "Rarm": 3, "Lelbow": 12, "Relbow": 14}
CLIPS.append(("Idle", 90, idle, True))
# Длительность клипа подобрана так, чтобы естественная скорость = шаг / (доля опоры * период)
# совпадала со скоростями в PlayerMovement: Walk 1.46, Run 4.07, Sprint 6.63, CrouchWalk 1.22 м/с.
CLIPS.append(("Walk", 22, lambda t: gait(t, 0.62, 0.08, 0.58, -0.045, 0.012, 3.0, 22, 18), True))
CLIPS.append(("Run", 20, lambda t: gait(t, 0.95, 0.20, 0.35, -0.075, 0.025, 9.0, 40, 70), True))
CLIPS.append(("Sprint", 21, lambda t: gait(t, 1.30, 0.28, 0.28, -0.090, 0.030, 15.0, 55, 85), True))

def crouch_base(extra):
    P = {"hip_z": -0.31, "hip_pitch": 18, "spine": 10, "chest": 6, "head": -24, "arm_down": 70,
         "Larm": 18, "Rarm": 18, "Lelbow": 35, "Relbow": 35, "leg_out": 6.0, "Lfoot": (0.06, 0), "Rfoot": (-0.08, 0)}
    P.update(extra); return P
CLIPS.append(("CrouchIdle", 80, lambda t: crouch_base({"chest": 6 + 1.2 * math.sin(2 * math.pi * t),
                                                       "hip_z": -0.31 + 0.005 * math.sin(2 * math.pi * t)}), True))
def crouch_walk(t):
    P = gait(t, 0.48, 0.08, 0.62, -0.30, 0.010, 0, 12, 35)
    P.update({"hip_pitch": 18, "spine": 10, "chest": 6, "head": -24, "arm_down": 70, "leg_out": 6.0})
    P["Larm"] += 16; P["Rarm"] += 16
    return P
CLIPS.append(("CrouchWalk", 19, crouch_walk, True))

def lerp(a, b, t): return a + (b - a) * t
def jump_start(t):
    # присед -> толчок
    c = math.sin(math.pi * min(t / 0.6, 1.0)) if t < 0.6 else 0.0
    up = ease(max(0.0, (t - 0.55) / 0.45))
    return {"hip_z": -0.14 * c + 0.02 * up, "hip_pitch": 12 * c, "spine": 8 * c, "head": -10 * c,
            "arm_down": lerp(74, 40, up), "Larm": lerp(-25 * c, 35, up), "Rarm": lerp(-25 * c, 35, up),
            "Lelbow": 25, "Relbow": 25, "Lfoot": (0.02, 0), "Rfoot": (-0.02, 0),
            "Lfoot_pitch": -25 * up, "Rfoot_pitch": -25 * up}
CLIPS.append(("JumpStart", 9, jump_start, False))
def jump_air(t):
    w = math.sin(2 * math.pi * t)
    return {"hip_z": 0.0, "spine": 4, "head": -4, "arm_down": 45 + 4 * w, "Larm": 20 + 6 * w, "Rarm": 14 - 6 * w,
            "Lelbow": 30, "Relbow": 30, "Lfoot": (0.10, 0.22 + 0.02 * w), "Rfoot": (-0.06, 0.12 - 0.02 * w),
            "Lfoot_pitch": 10, "Rfoot_pitch": -15}
CLIPS.append(("JumpAir", 30, jump_air, True))
def land(t):
    c = math.sin(math.pi * min(t / 0.9, 1.0)) * (1 - 0.3 * t)
    return {"hip_z": -0.20 * c - 0.012 * (1 - c), "hip_pitch": 14 * c, "spine": 10 * c + 1.5, "head": -12 * c,
            "arm_down": lerp(55, 76, t), "Larm": 15 * c, "Rarm": 15 * c, "Lelbow": 12 + 20 * c, "Relbow": 14 + 20 * c,
            "Lfoot": (0.03, 0), "Rfoot": (-0.04, 0), "leg_out": 4.0}
CLIPS.append(("Land", 12, land, False))

ACTIONS = [make_clip(*c) for c in CLIPS]

# ---------------------------------------------------------------- покой и экспорт
arm.animation_data.action = None
for pb in arm.pose.bones:
    pb.rotation_quaternion = (1, 0, 0, 0); pb.location = (0, 0, 0)

def export():
    os.makedirs(os.path.dirname(EXPORT_PATH), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True); body.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=EXPORT_PATH, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        use_mesh_modifiers=False, mesh_smooth_type='FACE', add_leaf_bones=False,
        primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, bake_anim_step=1.0)
    return EXPORT_PATH

bpy.app.driver_namespace["gw_pose"] = pose
bpy.app.driver_namespace["gw_export"] = export
print("GW character built:", len(me.vertices), "verts,", len(ACTIONS), "actions")
