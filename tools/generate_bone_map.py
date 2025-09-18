import math

import bpy
from mathutils import Euler, Matrix, Quaternion, Vector

globals().update(vars(bpy.data.texts["Text"].as_module()))


def format_quaternion_xyzw(quat, precision=3):
    # 格式化每個分量
    def format_component(val):
        # 先轉換成字串
        formatted = f"{val:.{precision}f}f"
        # 先處理 -0.000f
        formatted = formatted.replace("-0.000f", "0.000f")
        # 再處理正數前面加空格對齊
        if not formatted.startswith("-"):
            formatted = f" {formatted}"
        return formatted

    x_str = format_component(quat.x)
    y_str = format_component(quat.y)
    z_str = format_component(quat.z)
    w_str = format_component(quat.w)
    result = f"new Quaternion({x_str},{y_str},{z_str},{w_str})"
    return result


def calculate_bone_converter_quaternion(A):
    # --- 等價的矩陣實現步驟(參考MMD Tools) ---
    # 1. mat = A.to_matrix()
    # 2. mat[1], mat[2] = mat[2], mat[1]
    # 3. mat.transpose()
    # 4. mat.invert()
    # 5. B = mat.to_quaternion()
    c = 0.707106781186547524400844362104849039284835937688474
    w, x, y, z = A.w, A.x, A.y, A.z
    # 根據推導出的線性變換公式直接計算新四元數 B 的分量
    B = Quaternion(
        (
            c * (y - z),  # w分量
            c * (-y - z),  # x分量
            -c * (w - x),  # y分量
            c * (w + x),  # z分量
        )
    )
    return B


def unlock_and_setup_bones(armature):
    for bone in armature.pose.bones:
        bone.lock_rotation_w = False
        bone.lock_location[0] = False
        bone.lock_location[1] = False
        bone.lock_location[2] = False
        bone.lock_rotation[0] = False
        bone.lock_rotation[1] = False
        bone.lock_rotation[2] = False
        bone.lock_scale[0] = False
        bone.lock_scale[1] = False
        bone.lock_scale[2] = False


def disable_all_mmd_ik(armature):
    """關閉所有MMD IK"""
    disabled_count = 0
    for pose_bone in armature.pose.bones:
        if hasattr(pose_bone, "mmd_ik_toggle"):
            pose_bone.mmd_ik_toggle = False
            disabled_count += 1
    print(f"已關閉 {disabled_count} 個MMD IK")


def set_bone_world_direction(armature, bone_name, target_direction):
    """設定骨骼的世界方向"""
    if bone_name not in armature.pose.bones:
        print(f"骨骼 '{bone_name}' 不存在")
        return

    pose_bone = armature.pose.bones[bone_name]

    # 創建目標世界旋轉矩陣
    target_vector = Vector(target_direction).normalized()

    # 根據目標方向創建旋轉矩陣
    if target_vector == Vector((1, 0, 0)):  # 右方 (+X)
        target_euler = Euler((0, 0, -math.pi / 2), "XYZ")
    elif target_vector == Vector((-1, 0, 0)):  # 左方 (-X)
        target_euler = Euler((0, 0, math.pi / 2), "XYZ")
    elif target_vector == Vector((0, 1, 0)):  # 前方 (+Y)
        target_euler = Euler((0, 0, 0), "XYZ")
    elif target_vector == Vector((0, -1, 0)):  # 後方 (-Y)
        target_euler = Euler((0, 0, math.pi), "XYZ")
    elif target_vector == Vector((0, 0, 1)):  # 上方 (+Z)
        target_euler = Euler((math.pi / 2, 0, 0), "XYZ")
    elif target_vector == Vector((0, 0, -1)):  # 下方 (-Z)
        target_euler = Euler((-math.pi / 2, 0, 0), "XYZ")
    else:
        print(f"不支援的方向: {target_direction}")
        return

    # 創建目標世界矩陣
    target_world_matrix = target_euler.to_matrix().to_4x4()
    target_world_matrix.translation = pose_bone.matrix.translation

    # 設定世界矩陣，讓Blender自動計算正確的局部旋轉
    pose_bone.matrix = target_world_matrix

    print(f"骨骼 '{bone_name}' 已設定為朝向 {target_direction}")


def rotate_bone_around_y_axis(armature, bone_name, degrees):
    """沿著骨骼的Y軸（主軸）扭轉指定度數 - 方法二：矩陣局部空間旋轉"""
    if bone_name not in armature.pose.bones:
        print(f"骨骼 '{bone_name}' 不存在")
        return

    if degrees == 0:
        return

    pose_bone = armature.pose.bones[bone_name]

    # 方法2：在骨骼局部空間中應用Y軸旋轉矩陣
    current_world_matrix = pose_bone.matrix.copy()

    # 創建Y軸旋轉矩陣
    y_rotation = Matrix.Rotation(math.radians(degrees), 4, "Y")

    # 在局部空間中應用旋轉（右乘）
    rotated_matrix = current_world_matrix @ y_rotation

    # 設定新的世界矩陣
    pose_bone.matrix = rotated_matrix

    print(f"骨骼 '{bone_name}' 沿Y軸扭轉 {degrees}°")


def output_bone_mapping_info(armature, bone_names):
    """第三步驟：輸出骨骼映射資訊"""
    print("\n=== 第三步驟：輸出骨骼映射資訊 ===")

    for bone_name in bone_names:
        if bone_name not in armature.pose.bones:
            result = "new Quaternion( 0.000f,  0.000f,  0.000f,  1.000f), new Quaternion( 0.000f,  0.000f,  0.000f,  1.000f)"
            print(f"{result} {bone_name}")
            continue

        pose_bone = armature.pose.bones[bone_name]

        # restPoseCorrection(目前的骨骼旋轉)
        restPoseCorrection = pose_bone.rotation_quaternion.copy()

        # 特殊骨骼處理
        if bone_name in {"左目", "右目"}:  # 尊重眼睛原來的旋轉
            restPoseCorrection = Quaternion((1, 0, 0, 0))

        # coordinateConversion
        local_rotation_quat = pose_bone.bone.matrix_local.to_quaternion()
        coordinateConversion = calculate_bone_converter_quaternion(local_rotation_quat)

        result = f"{format_quaternion_xyzw(restPoseCorrection)}, {format_quaternion_xyzw(coordinateConversion)} {bone_name}"
        print(result)


def bone_hierarchy_path(armature, bone_name):
    """建立從根骨骼到目標骨骼的路徑，用於父子階層排序"""
    if bone_name not in armature.pose.bones:
        return tuple()

    bone = armature.pose.bones[bone_name].bone
    path = []
    while bone:
        path.append(bone.name)
        bone = bone.parent
    return tuple(reversed(path))


def align_all_bones():
    """按照階層順序調整所有骨骼方向，然後進行Y軸扭轉，最後輸出映射資訊"""
    armature = bpy.context.active_object
    if not armature or armature.type != "ARMATURE":
        print("請選擇一個骨架物件")
        return

    if bpy.context.mode != "POSE":
        bpy.ops.object.mode_set(mode="POSE")

    # 骨骼方向對應表，格式：(骨骼名稱, 世界方向, Y軸扭轉度數)
    bone_directions = [
        ("全ての親", (0, 0, 1), 0),
        ("センター", (0, 0, -1), 0),
        ("左目", (0, -1, 0), -90),
        ("右目", (0, -1, 0), -90),
        ("首", (0, 0, 1), 0),
        ("頭", (0, 0, 1), 0),
        ("上半身", (0, 0, 1), 0),
        ("上半身2", (0, 0, 1), 0),
        ("下半身", (0, 0, -1), 0),
        ("左肩", (1, 0, 0), 180),
        ("右肩", (-1, 0, 0), 0),
        ("左腕", (1, 0, 0), 0),
        ("右腕", (-1, 0, 0), 0),
        ("左ひじ", (1, 0, 0), 0),
        ("右ひじ", (-1, 0, 0), 0),
        ("左手首", (1, 0, 0), 0),
        ("右手首", (-1, 0, 0), 0),
        ("左足", (0, 0, -1), 0),
        ("右足", (0, 0, -1), 0),
        ("左ひざ", (0, 0, -1), 0),
        ("右ひざ", (0, 0, -1), 0),
        ("左足首", (0, -1, 0), 180),
        ("右足首", (0, -1, 0), 180),
        ("左足先EX", (0, -1, 0), 180),
        ("右足先EX", (0, -1, 0), 180),
        ("左親指０", (1, 0, 0), -90),
        ("左親指１", (1, 0, 0), 0),
        ("左親指２", (1, 0, 0), 0),
        ("左人指１", (1, 0, 0), 0),
        ("左人指２", (1, 0, 0), 0),
        ("左人指３", (1, 0, 0), 0),
        ("左中指１", (1, 0, 0), 0),
        ("左中指２", (1, 0, 0), 0),
        ("左中指３", (1, 0, 0), 0),
        ("左薬指１", (1, 0, 0), 0),
        ("左薬指２", (1, 0, 0), 0),
        ("左薬指３", (1, 0, 0), 0),
        ("左小指１", (1, 0, 0), 0),
        ("左小指２", (1, 0, 0), 0),
        ("左小指３", (1, 0, 0), 0),
        ("右親指０", (-1, 0, 0), -90),
        ("右親指１", (-1, 0, 0), 0),
        ("右親指２", (-1, 0, 0), 0),
        ("右人指１", (-1, 0, 0), 180),
        ("右人指２", (-1, 0, 0), 0),
        ("右人指３", (-1, 0, 0), 0),
        ("右中指１", (-1, 0, 0), 180),
        ("右中指２", (-1, 0, 0), 0),
        ("右中指３", (-1, 0, 0), 0),
        ("右薬指１", (-1, 0, 0), 180),
        ("右薬指２", (-1, 0, 0), 0),
        ("右薬指３", (-1, 0, 0), 0),
        ("右小指１", (-1, 0, 0), 180),
        ("右小指２", (-1, 0, 0), 0),
        ("右小指３", (-1, 0, 0), 0),
    ]

    # 備份排序前的骨骼名稱列表
    bone_names_unsort = [bone_name for bone_name, _, _ in bone_directions]

    print("=== 第負二步：按階層順序排序骨骼 ===")
    # 第-2步：使用階層路徑對bone_directions進行排序
    bone_directions.sort(key=lambda x: bone_hierarchy_path(armature, x[0]))
    # 輸出排序結果
    print("骨骼處理順序（按階層排序）：")
    for i, (bone_name, direction, y_rotation) in enumerate(bone_directions):
        hierarchy_path = bone_hierarchy_path(armature, bone_name)
        print(f"{i + 1:2d}. {bone_name} (階層深度: {len(hierarchy_path)})")

    print("=== 第負一步：關閉所有MMD IK ===")
    # 第-1步：關閉所有MMD IK
    disable_all_mmd_ik(armature)
    bpy.context.view_layer.update()

    print("=== 第零步：設定骨骼鎖定屬性 ===")
    # 第零步：解鎖骨骼鎖定，方便CtrlRGS
    unlock_and_setup_bones(armature)
    bpy.context.view_layer.update()

    print("=== 第一步：調整骨骼方向 ===")
    # 第一步：調整骨骼方向
    for bone_name, direction, y_rotation in bone_directions:
        set_bone_world_direction(armature, bone_name, direction)

    print("=== 第二步：沿Y軸扭轉 ===")
    # 第二步：對需要的骨骼進行Y軸扭轉
    for bone_name, direction, y_rotation in bone_directions:
        if y_rotation != 0:
            rotate_bone_around_y_axis(armature, bone_name, y_rotation)

    # 更新場景
    bpy.context.view_layer.update()

    # 第三步：輸出骨骼映射資訊
    output_bone_mapping_info(armature, bone_names_unsort)

    print("\n所有骨骼方向調整完成!")


# 執行腳本
align_all_bones()
