from pathlib import Path
import sys
from PIL import Image

source = Path(sys.argv[1])
output = Path(sys.argv[2])
output.mkdir(parents=True, exist_ok=True)
for folder, filename, duration in [
    ('animated', 'temas-animados-hd.gif', 80),
    ('all-themes', 'dock-15-temas-hd.gif', 120),
]:
    files = sorted((source / folder).glob('*.png'))
    expected = 96 if folder == 'animated' else 360
    if len(files) != expected:
        raise ValueError(f'{folder}: expected {expected} frames, found {len(files)}')
    # One palette for complete sequence prevents colors flickering between frames.
    samples = files[::max(1, len(files) // 24)]
    thumbs = []
    for path in samples:
        with Image.open(path) as im:
            thumb = im.convert('RGB')
            thumb.thumbnail((480, 480))
            thumbs.append(thumb)
    sw, sh = max(im.width for im in thumbs), sum(im.height for im in thumbs)
    sheet = Image.new('RGB', (sw, sh), '#11171b')
    y = 0
    for thumb in thumbs:
        sheet.paste(thumb, (0, y))
        y += thumb.height
    palette = sheet.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    frames = []
    for path in files:
        with Image.open(path) as im:
            frames.append(im.convert('RGB').quantize(palette=palette, dither=Image.Dither.NONE))
    dest = output / filename
    frames[0].save(dest, save_all=True, append_images=frames[1:], duration=duration,
                   loop=0, disposal=1, optimize=True)
    with Image.open(dest) as gif:
        count = gif.n_frames
        assert gif.size == ((1680, 2040) if folder == 'animated' else (1920, 540))
        assert count > 1
        assert gif.info['loop'] == 0
        for index in [0, count // 2, count - 1]:
            gif.seek(index)
            gif.convert('RGB').save(output / f'{dest.stem}-check-{index}.png')
        print(f'{filename}: {gif.size[0]}x{gif.size[1]}, {count} frames, {dest.stat().st_size / 1048576:.2f} MiB', flush=True)
