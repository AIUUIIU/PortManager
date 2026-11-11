"""生成 Port Manager 应用图标（纯标准库，SDF 超采样抗锯齿，输出多尺寸 ICO）。

用法：在仓库任意位置执行 `python tools/make_icon.py`
输出：Assets/AppIcon.ico 与 docs/screenshots/app-icon.png
"""
import struct, zlib, math, os

SS = 4  # 超采样倍数

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")
DOCS = os.path.join(ROOT, "docs", "screenshots")


def clamp(v, a=0.0, b=1.0):
    return a if v < a else (b if v > b else v)


def sd_round_rect(px, py, cx, cy, hw, hh, r):
    qx = abs(px - cx) - (hw - r)
    qy = abs(py - cy) - (hh - r)
    ax, ay = max(qx, 0.0), max(qy, 0.0)
    return math.sqrt(ax * ax + ay * ay) + min(max(qx, qy), 0.0) - r


def sd_capsule(px, py, x1, y1, x2, y2, r):
    dx, dy = x2 - x1, y2 - y1
    L2 = dx * dx + dy * dy
    t = 0.0 if L2 == 0 else clamp(((px - x1) * dx + (py - y1) * dy) / L2)
    qx, qy = px - (x1 + t * dx), py - (y1 + t * dy)
    return math.sqrt(qx * qx + qy * qy) - r


def lerp_color(c1, c2, t):
    return tuple(int(round(c1[i] + (c2[i] - c1[i]) * t)) for i in range(3))


def render(size):
    """在 size*SS 的画布上绘制后盒式降采样"""
    N = size * SS
    s = N / 1024.0
    out = bytearray()
    bg1 = (0x4C, 0x9A, 0xFF)
    bg2 = (0x14, 0x4E, 0xC8)

    for y in range(size):
        out.append(0)  # PNG filter type 0
        for x in range(size):
            r = g = b = a = 0.0
            for sy in range(SS):
                for sx in range(SS):
                    px = (x * SS + sx + 0.5) / s
                    py = (y * SS + sy + 0.5) / s

                    # 背景圆角方块
                    d_bg = sd_round_rect(px, py, 512, 512, 480, 480, 210)
                    bg_a = clamp(0.5 - d_bg)
                    if bg_a <= 0:
                        continue
                    t = clamp(((px - 32) + (py - 32)) / 1984.0)
                    col = lerp_color(bg1, bg2, t)

                    # 白色插头图形
                    d = sd_round_rect(px, py, 512, 588, 186, 136, 58)      # 插头主体
                    d = min(d, sd_capsule(px, py, 432, 302, 432, 452, 28))  # 左插脚
                    d = min(d, sd_capsule(px, py, 592, 302, 592, 452, 28))  # 右插脚
                    d = min(d, sd_capsule(px, py, 512, 724, 512, 878, 30))  # 线缆
                    fg_a = clamp(0.5 - d)
                    if fg_a > 0:
                        col = lerp_color(col, (255, 255, 255), fg_a)

                    r += col[0] * bg_a
                    g += col[1] * bg_a
                    b += col[2] * bg_a
                    a += bg_a

            n = SS * SS
            if a > 0:
                out += bytes((int(round(r / a)), int(round(g / a)), int(round(b / a)), int(round(255 * a / n))))
            else:
                out += b"\x00\x00\x00\x00"
    return bytes(out)


def png_bytes(size, raw):
    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    ihdr = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def main():
    sizes = [16, 24, 32, 48, 64, 128, 256]
    frames = []
    for s in sizes:
        raw = render(s)
        frames.append((s, png_bytes(s, raw)))
        print("rendered", s)

    os.makedirs(DOCS, exist_ok=True)

    # 预览大图
    preview_path = os.path.join(DOCS, "app-icon.png")
    with open(preview_path, "wb") as f:
        f.write(png_bytes(256, render(256)))

    header = struct.pack("<HHH", 0, 1, len(frames))
    offset = 6 + 16 * len(frames)
    entries, blobs = b"", b""
    for s, data in frames:
        w = 0 if s >= 256 else s
        entries += struct.pack("<BBBBHHII", w, w, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
        blobs += data

    ico_path = os.path.join(ASSETS, "AppIcon.ico")
    with open(ico_path, "wb") as f:
        f.write(header + entries + blobs)
    print("wrote Assets/AppIcon.ico", os.path.getsize(ico_path), "bytes")


main()
