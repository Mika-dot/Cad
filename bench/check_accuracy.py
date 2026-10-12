"""Independent deterministic 3-D equal-memory counterexample in the report.

Does NOT assume that a single ray proves a global Hausdorff comparison.
Standard library only. Intended for CI and manual verification.
"""
import math

R = 3.0
domain_length = 8.0
fvm_count = 8
rgb_channels = 5
bits_per_channel = 24
fvm_bytes = fvm_count ** 3 * rgb_channels * bits_per_channel // 8
binary_count = 39
binary_bytes = math.ceil(binary_count ** 3 / 8)
assert fvm_bytes == 7680
assert binary_bytes == 7415
assert math.ceil((binary_count + 1) ** 3 / 8) > fvm_bytes

def sphere(x, y, z):
    return R * R - x * x - y * y - z * z

# FV voxel: x in [3,4], y and z in [0,1].
px, py, pz = 3.5, 0.5, 0.5
value = sphere(px, py, pz)
gradient = (-2 * px, -2 * py, -2 * pz)
assert value == -3.75
assert gradient == (-7.0, -1.0, -1.0)

# F_linear(x,0,0) = value + gradient_x*(x-px)
#                       + gradient_y*(0-py) + gradient_z*(0-pz).
intercept = value - gradient[0]*px - gradient[1]*py - gradient[2]*pz
fvm_zero_x = -intercept / gradient[0]
assert abs(intercept - 21.75) < 1e-12
assert 3.0 < fvm_zero_x < 4.0
fvm_error = abs(fvm_zero_x - R)

# Packed occupancy lattice with 39 samples per axis and centers at y=z=0.
step_binary = domain_length / binary_count
last_inside = -4.0 + (33 + 0.5)*step_binary
first_outside = -4.0 + (34 + 0.5)*step_binary
binary_surface_x = -4.0 + 34*step_binary
assert sphere(last_inside, 0, 0) > 0
assert sphere(first_outside, 0, 0) < 0
binary_error = abs(binary_surface_x - R)

assert abs(fvm_error - 3/28) < 1e-12
assert abs(binary_error - 1/39) < 1e-12
assert fvm_error / binary_error > 4
# A second, independent precision counterexample for 5-channel RGB24.
# Constant F = 10^9: (0,0,0,1,-10^9) normalized as a single 5-vector.
constant = 1_000_000_000.0
length = math.hypot(1.0, constant)
m_u = 1.0 / length
m_free = -constant / length
max_rgb = 2**24 - 1

def encode_rgb24(value):
    # Python round() and C# Math.Round() both use ties-to-even by default.
    return round((value + 1.0) * max_rgb / 2.0)

def decode_rgb24(code):
    return 2.0 * code / max_rgb - 1.0

colour_u = encode_rgb24(m_u)
colour_free = encode_rgb24(m_free)
restored_constant = -decode_rgb24(colour_free) / decode_rgb24(colour_u)
relative_loss = abs(restored_constant - constant) / constant
assert colour_u == 0x800000 and colour_free == 0
assert abs(restored_constant - 16777215.0) < 0.01
assert relative_loss > 0.98

print("EQUAL_MEMORY_PROOF_PASS")
print(f"FVM 8^3, 5 x RGB24: {fvm_bytes} B, zero_x={fvm_zero_x:.9f}, error={fvm_error:.9f} mm")
print(f"Bit-packed 39^3: {binary_bytes} B, zero_x={binary_surface_x:.9f}, error={binary_error:.9f} mm")
print(f"Error ratio FVM / binary = {fvm_error / binary_error:.6f}x on the selected ray")
print(f"RGB24_PRECISION_CASE_PASS: original={constant:.0f}; restored={restored_constant:.3f}; relative_error={relative_loss:.3%}")
