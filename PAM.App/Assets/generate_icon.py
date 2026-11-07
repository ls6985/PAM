"""绘制 PAM.App 的多尺寸程序图标 Assets/App.ico。

不需要任何图片素材：深色圆角底 + 亮蓝色 </> 全部用 Pillow 按几何坐标绘制，
再手工组装成标准 ICO（32bpp DIB 条目，csc 的 /win32icon 可直接吃）。
改配色改上面的 BG / FG，改形状改 round_line 里的相对坐标，然后重新运行即可。

    pip install pillow
    python Assets/generate_icon.py

可选：设置环境变量 PAM_ICON_PREVIEW 为 png 路径，额外导出一张各尺寸预览图。
"""

import os
import struct

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_ICO = os.path.join(HERE, "App.ico")

SIZES = [16, 24, 32, 48, 64, 128, 256]
BG = (30, 37, 54, 255)
FG = (108, 196, 255, 255)


def draw_icon(size):
    """按 size 单独绘制（不靠大图缩小），保证 16x16 下线条也清晰。"""
    ss = size * 4
    im = Image.new("RGBA", (ss, ss), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, ss - 1, ss - 1], radius=round(ss * 0.22), fill=BG)
    width = max(2, round(ss * 0.07))
    radius = width / 2

    def round_line(points):
        pts = [(round(x * ss), round(y * ss)) for x, y in points]
        d.line(pts, fill=FG, width=width, joint="curve")
        for x, y in pts:
            d.ellipse([x - radius, y - radius, x + radius, y + radius], fill=FG)

    round_line([(0.36, 0.27), (0.17, 0.50), (0.36, 0.73)])
    round_line([(0.43, 0.80), (0.59, 0.20)])
    round_line([(0.64, 0.27), (0.83, 0.50), (0.64, 0.73)])
    return im.resize((size, size), Image.LANCZOS)


def make_dib(im):
    """转成 ICO 里的 32bpp DIB：BITMAPINFOHEADER + 自下而上的 BGRA + AND 掩码。"""
    w, h = im.size
    pixels = list(im.convert("RGBA").getdata())
    xor = bytearray()
    mask = bytearray()
    row_bytes = ((w + 31) // 32) * 4
    for y in range(h - 1, -1, -1):
        row = pixels[y * w:(y + 1) * w]
        for r, g, b, a in row:
            xor += bytes((b, g, r, a))
        bits = bytearray(row_bytes)
        for x, (_, _, _, a) in enumerate(row):
            if a < 128:
                bits[x // 8] |= 0x80 >> (x % 8)
        mask += bits
    header = struct.pack(
        "<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0,
        len(xor) + len(mask), 0, 0, 0, 0,
    )
    return header + bytes(xor) + bytes(mask)


def build_ico():
    parts = [make_dib(draw_icon(size)) for size in SIZES]
    offset = 6 + 16 * len(SIZES)
    entries = bytearray()
    payload = bytearray()
    for size, part in zip(SIZES, parts):
        dimension = 0 if size >= 256 else size
        entries += struct.pack(
            "<BBBBHHII", dimension, dimension, 0, 0, 1, 32, len(part), offset
        )
        payload += part
        offset += len(part)

    with open(OUT_ICO, "wb") as f:
        f.write(struct.pack("<HHH", 0, 1, len(SIZES)))
        f.write(entries)
        f.write(payload)
    print(f"wrote {OUT_ICO} ({os.path.getsize(OUT_ICO)} bytes, {len(SIZES)} sizes)")


def build_preview(path):
    font_path = r"C:\Windows\Fonts\msyh.ttc"
    title_font = ImageFont.truetype(font_path, 19)
    label_font = ImageFont.truetype(font_path, 14)
    note_font = ImageFont.truetype(font_path, 13)
    canvas = Image.new("RGB", (700, 430), "#F7F8FA")
    d = ImageDraw.Draw(canvas)
    d.text((36, 24), "PAM.App 程序图标 · 全部由代码绘制，无需图片素材",
           font=title_font, fill="#2C2C2A")
    for size, x in [(256, 40), (64, 340), (48, 430), (32, 510), (16, 575)]:
        icon = draw_icon(size)
        y = 80 + (256 - size) // 2
        d.rectangle([x - 8, y - 8, x + size + 7, y + size + 7],
                    fill="#FFFFFF", outline="#D3D1C7")
        canvas.paste(icon, (x, y), icon)
        d.text((x, 356), f"{size}×{size}", font=label_font, fill="#5F5E5A")
    d.text((36, 392),
           "深色圆角底 + 亮蓝色 </>  ·  打包为多尺寸 App.ico，任务栏 / Alt+Tab / 资源管理器统一生效",
           font=note_font, fill="#5F5E5A")
    canvas.save(path)
    print(f"wrote preview {path}")


if __name__ == "__main__":
    build_ico()
    preview = os.environ.get("PAM_ICON_PREVIEW")
    if preview:
        build_preview(preview)
