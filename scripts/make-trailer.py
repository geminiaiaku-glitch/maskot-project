"""Render an original product trailer from Momo vectors and verified native WPF shots."""
from __future__ import annotations

import argparse
import functools
import hashlib
import io
import json
import math
from pathlib import Path
import subprocess
import urllib.request
import wave
import xml.etree.ElementTree as ET
import zipfile

import cairosvg
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
W, H, FPS, DURATION = 1920, 1080, 30, 32
SOURCE_TAG = "v0.4.0-preview.2"
SOURCE_SHA256 = "278264749559441040acc88a8e03ee58c36a558034741062ead7dfa3a4449e22"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"
NS = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
CUTS = [0, 4, 8, 13.5, 19, 24, 28, 32]
WHITE, MUTED, PEACH, BLUE = "#F9F5ED", "#AEB5C4", "#EFD4B3", "#ACCAEF"


def ease(t):
    t = max(0, min(1, t))
    return 1 - (1 - t) ** 3


@functools.lru_cache(maxsize=48)
def font(size, weight="regular"):
    name = {"regular": "Regular", "bold": "Semibold", "light": "Light"}[weight]
    path = Path(f"/usr/share/fonts/truetype/open-sans/OpenSans-{name}.ttf")
    if not path.exists():
        path = Path("/usr/share/fonts/truetype/dejavu/DejaVuSans" + ("-Bold" if weight == "bold" else "") + ".ttf")
    return ImageFont.truetype(str(path), size)


def label(text, size=28, color=WHITE, weight="regular", spacing=0):
    lines = text.split("\n")
    face = font(size, weight)
    widths = [int(face.getlength(line) + max(0, len(line) - 1) * spacing) for line in lines]
    im = Image.new("RGBA", (max(widths + [1]) + 10, int(len(lines) * size * 1.42 + 12)))
    d = ImageDraw.Draw(im)
    for i, line in enumerate(lines):
        x = 2
        if spacing:
            for ch in line:
                d.text((x, i * size * 1.42), ch, font=face, fill=color)
                x += face.getlength(ch) + spacing
        else:
            d.text((x, i * size * 1.42), line, font=face, fill=color)
    return im


def place(base, layer, x, y, alpha=1, width=None):
    if alpha <= 0:
        return
    if width is not None:
        width = max(1, int(width))
        height = max(1, round(layer.height * width / layer.width))
        if layer.size != (width, height):
            layer = layer.resize((width, height), Image.Resampling.LANCZOS)
    if alpha < 1:
        layer = layer.copy()
        layer.putalpha(layer.getchannel("A").point(lambda v: int(v * alpha)))
    base.alpha_composite(layer, (round(x), round(y)))


@functools.lru_cache(maxsize=12)
def background(theme):
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    rgb = np.zeros((H, W, 3), dtype=np.float32) + np.array([8, 12, 20])
    if theme == "warm": colors = [(1300, 480, [47, 29, 22]), (300, 900, [12, 17, 30])]
    elif theme == "blue": colors = [(1380, 550, [22, 39, 69]), (220, 900, [22, 16, 34])]
    elif theme == "rose": colors = [(1280, 560, [60, 25, 33]), (120, 100, [16, 23, 36])]
    else: colors = [(1300, 460, [26, 45, 43]), (200, 880, [24, 20, 39])]
    for cx, cy, color in colors:
        radial = np.exp(-((xx - cx) ** 2 / 620**2 + (yy - cy) ** 2 / 560**2))
        rgb += radial[:, :, None] * color
    rgb = np.clip(rgb, 0, 255).astype(np.uint8)
    im = Image.fromarray(rgb).convert("RGBA")
    d = ImageDraw.Draw(im)
    d.line((145, 970, 1775, 970), fill=(130, 149, 175, 35), width=1)
    return im


def prepare_assets(directory):
    if directory:
        return Path(directory)
    dest = ROOT / "artifacts/trailer-source"
    dest.mkdir(parents=True, exist_ok=True)
    archive = dest / "source.zip"
    digest = hashlib.sha256()
    with urllib.request.urlopen(f"https://github.com/geminiaiaku-glitch/maskot-project/releases/download/{SOURCE_TAG}/Maskot-win-x64.zip", timeout=90) as r, archive.open("wb") as out:
        while chunk := r.read(1024 * 1024): out.write(chunk); digest.update(chunk)
    if digest.hexdigest() != SOURCE_SHA256:
        raise RuntimeError("Native screenshot source checksum mismatch")
    with zipfile.ZipFile(archive) as z:
        for name in z.namelist():
            if "windows-smoke/" in name and name.endswith(".png"):
                (dest / Path(name).name).write_bytes(z.read(name))
    return dest


# Draw the same original articulated vectors used by MascotView, at trailer resolution.
GEOMETRIES = {e.attrib[X + "Key"]: e.attrib["Figures"] for e in ET.parse(ROOT / "src/Maskot.Windows/Assets/Momo.xaml").getroot() if e.tag == NS + "PathGeometry"}
VIEW = ET.parse(ROOT / "src/Maskot.Windows/Views/MascotView.xaml").getroot().find(".//" + NS + "Canvas")


def frame_values(pose, t):
    breath = math.sin(t * math.tau / 3.2)
    stride = math.sin(t * math.tau / .72)
    blink = t % 4.1
    eyes = .08 + .92 * abs(blink - .07) / .07 if blink < .14 else 1
    f = dict(Bob=breath * 2.8, Lean=0, Head=math.sin(t * 1.2) * 3,
             LeftFoot=0, RightFoot=0, LeftArm=breath * 3, RightArm=-breath * 3,
             Tail=math.sin(t * math.tau / 2.4) * 15, Eyes=eyes, Breath=1 + breath * .025, MouthOpening=0)
    if pose == "Walk": f.update(Bob=-2-abs(stride)*4.5, Lean=stride*2.5, Head=-stride*2, LeftFoot=stride*24, RightFoot=-stride*24, LeftArm=-stride*13, RightArm=stride*13, Breath=1)
    if pose == "Greet": f.update(Bob=-2+breath*2.2, Head=math.sin(t*2)*7, LeftArm=0, RightArm=-35+math.sin(t*10)*18, Breath=1+breath*.02)
    if pose == "Singing": f.update(Head=f["Head"]+math.sin(t*5)*4, RightArm=-28+math.sin(t*4)*4, MouthOpening=.25+.75*abs(math.sin(t*8)))
    if pose == "Confused": f.update(Head=13+math.sin(t*1.8)*7, LeftArm=-16+math.sin(t*2.4)*8, RightArm=8+math.sin(t*2.7)*8)
    f["MicLift"] = f["RightArm"]
    return f


def svg_node(node, f, pose):
    kind = node.tag.removeprefix(NS)
    if "." in kind:
        return ""
    a = node.attrib
    name = a.get(X + "Name", "")
    show = {"Microphone": pose in ("Singing", "Confused"), "SingingMouth": pose == "Singing", "NormalMouth": pose not in ("Singing", "Confused"), "ConfusedBrows": pose == "Confused", "ConfusedMouth": pose == "Confused", "MusicNote": pose == "Singing", "SleepMark": False, "Greeting": pose == "Greet"}
    if name in show and not show[name]: return ""
    transform = []
    left, top = float(a.get("Canvas.Left", 0)), float(a.get("Canvas.Top", 0))
    if left or top: transform.append(f"translate({left} {top})")
    rt = node.find(NS + kind + ".RenderTransform")
    if rt is not None:
        elements = list(rt)[0]
        parts = list(elements) if elements.tag == NS + "TransformGroup" else [elements]
        for part in reversed(parts):
            p = part.attrib; n = p.get(X + "Name", ""); tag = part.tag.removeprefix(NS)
            cx, cy = float(p.get("CenterX", 0)), float(p.get("CenterY", 0))
            if a.get("RenderTransformOrigin") == ".5,.5": cx, cy = float(a.get("Width", 0))/2, float(a.get("Height", 0))/2
            if tag == "RotateTransform": transform.append(f"rotate({f.get(n, float(p.get('Angle', 0)))} {cx} {cy})")
            elif tag == "TranslateTransform": transform.append(f"translate(0 {f.get(n, 0)})")
            elif tag == "ScaleTransform":
                sy = f.get(n, float(p.get("ScaleY", 1))); sx = float(p.get("ScaleX", 1))
                transform.append(f"translate({cx} {cy}) scale({sx} {sy}) translate({-cx} {-cy})")
    attrs = f' transform="{" ".join(transform)}"' if transform else ""
    if kind == "Canvas": return f"<g{attrs}>" + "".join(svg_node(ch, f, pose) for ch in node) + "</g>"
    style = f' fill="{a.get("Fill", "none")}" stroke="{a.get("Stroke", "none")}" stroke-width="{a.get("StrokeThickness", 1)}" stroke-linecap="round" stroke-linejoin="round"'
    if kind == "Path":
        data = a["Data"]
        if data.startswith("{StaticResource "): data = GEOMETRIES[data[len("{StaticResource "):-1]]
        return f'<g{attrs}><path d="{data}"{style}/></g>'
    if kind == "Ellipse":
        w, h = float(a["Width"]), float(a["Height"])
        return f'<g{attrs}><ellipse cx="{w/2}" cy="{h/2}" rx="{w/2}" ry="{h/2}"{style}/></g>'
    if kind == "TextBlock":
        return f'<g{attrs}><text y="{float(a.get("FontSize",15))}" font-size="{a.get("FontSize",15)}" font-family="DejaVu Sans" fill="{a.get("Foreground",WHITE)}">{a.get("Text", "")}</text></g>'
    return ""


@functools.lru_cache(maxsize=80)
def mascot(pose, frame):
    t = frame / FPS
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="468" height="635" viewBox="-5 -5 122 165">{svg_node(VIEW, frame_values(pose,t), pose)}</svg>'
    return Image.open(io.BytesIO(cairosvg.svg2png(bytestring=svg.encode()))).convert("RGBA")


def soundtrack(path):
    sr = 48000
    rng = np.random.default_rng(29)
    length = DURATION * sr
    stereo = np.zeros((length, 2), dtype=np.float64)
    beat = 60 / 90
    def tone(start, midi, duration, amplitude, pan=0, bell=False):
        begin = int(start*sr); count = min(int(duration*sr), length-begin)
        if count <= 0: return
        t = np.arange(count)/sr; hz = 440*2**((midi-69)/12)
        if bell:
            s = (np.sin(2*np.pi*hz*t)*np.exp(-t*3) + .24*np.sin(2*np.pi*hz*2*t)*np.exp(-t*6))
            env = np.minimum(t/.012,1) * np.minimum((duration-t)/.08,1)
        else:
            s = np.sin(2*np.pi*hz*t)+.14*np.sin(2*np.pi*hz*2*t+.15)
            env = np.minimum(t/.18,1)*np.minimum((duration-t)/.5,1)*np.exp(-t*.22)
        s *= np.maximum(0,env)*amplitude
        stereo[begin:begin+count,0] += s*math.sqrt((1-pan)/2)
        stereo[begin:begin+count,1] += s*math.sqrt((1+pan)/2)
    chords = [(48,55,59,64),(45,52,55,60),(41,48,52,57),(43,50,55,59)]
    for bar in range(12):
        start=bar*4*beat; notes=chords[bar%4]
        for i,n in enumerate(notes): tone(start+i*.024,n+12,3.8,.024,(i-1.5)*.28)
        tone(start,notes[0]-12,2.3,.06,0)
        for k in range(8): tone(start+k*beat/2,notes[(k+bar)%4]+24,.7,.025,(-1 if k%2 else 1)*.3,True)
        for k in (0,2):
            i=int((start+k*beat)*sr); count=min(int(.18*sr),length-i)
            if count>0:
                t=np.arange(count)/sr; kick=np.sin(2*np.pi*(52*t+3*(1-np.exp(-t*28))))*np.exp(-t*24)*.08
                stereo[i:i+count]+=kick[:,None]
    # Original four-note motif, sparse and soft.
    for start,midi in [(1.33,76),(2.0,79),(3.0,74),(5.33,72),(8,76),(9.33,79),(11.0,81),(14,79),(16,76),(18.0,74),(21.33,72),(24.0,76),(26,79),(28.66,76),(30,72)]: tone(start,midi,1.7,.045,.12,True)
    for start in [4.8,8.5,13.7,19.1,24.1,28.1]:
        i=int(start*sr); n=int(.18*sr); t=np.arange(n)/sr
        whoosh=rng.normal(0,1,n)*np.sin(np.pi*t/.18)**2*.004
        stereo[i:i+n] += whoosh[:,None]
    # Short stereo echoes; no samples or external music.
    dry=stereo.copy()
    for delay,gain in [(.13,.14),(.27,.10),(.41,.06)]:
        n=int(delay*sr); stereo[n:,0]+=dry[:-n,1]*gain; stereo[n:,1]+=dry[:-n,0]*gain
    t=np.arange(length)/sr; fade=np.minimum(t/.7,1)*np.minimum((DURATION-t)/2.5,1)
    stereo*=fade[:,None]; stereo=np.tanh(stereo*1.3)
    samples=(np.clip(stereo,-1,1)*32767).astype('<i2')
    with wave.open(str(path),'wb') as out: out.setnchannels(2);out.setsampwidth(2);out.setframerate(sr);out.writeframes(samples.tobytes())


class Trailer:
    def __init__(self, assets):
        self.shots={n:Image.open(assets/n).convert('RGBA') for n in ['notch.png','notch-carousel-music.png','notch-carousel-lyrics.png','notch-carousel-focus.png','notch-carousel-space.png','momo-lyric-bubble.png']}
        self.titles={
            0:('Kenalan dengan','Momo','Teman kecil di desktopmu.'),
            1:('DI DESKTOPMU','Sedikit teman.\nBanyak suasana.','Berjalan. Bereaksi. Menemanimu.'),
            2:('NOTCH INTERAKTIF','Dekatkan kursor.\nBuka lebih banyak.','Musik, fokus, dan ruang untuk idemu.'),
            3:('MUSIK & LIRIK','Musik jalan.\nMomo ikut bernyanyi.','Lirik mengikuti lagu saat tersedia.'),
            4:('WAKTUNYA FOKUS','Satu hal.\nSatu waktu.','Atur ritmemu. Ambil jeda.'),
            5:('RUANG KECILMU','Ide tersimpan.\nPikiran lebih ringan.','Catatan, tugas, dan pintasan file.'),
            6:('UNTUK HARIMU','Satu teman kecil.\nBanyak cerita.','Momo untuk Windows.')}
        self.type={i:[label(a,20,BLUE if i in (2,3) else PEACH,'bold',2),label(b,108 if i==0 else 64 if i==3 else 72,WHITE,'bold'),label(c,28,MUTED)] for i,(a,b,c) in self.titles.items()}
        self.brand=label('M O M O',21,WHITE,'bold')
        self.cta=Image.new('RGBA',(360,72));d=ImageDraw.Draw(self.cta);d.rounded_rectangle((0,0,360,70),radius=35,fill='#F3E4D2');d.text((42,15),'Unduh di GitHub',font=font(27,'bold'),fill='#19202B')
        self.url=label('github.com/geminiaiaku-glitch/maskot-project',20,MUTED)
        self.demo=label('Tampilan aplikasi · data demo',16,'#7A8597')
        self.footer=label('WINDOWS  /  PORTABLE',17,'#9AA6B9','regular',2)

    def card(self, im, key, x, y, width, alpha=1):
        shot=self.shots[key]
        sh=round(shot.height*width/shot.width)
        shadow=Image.new('RGBA',(int(width)+100,sh+100));d=ImageDraw.Draw(shadow)
        d.rounded_rectangle((50,46,width+50,sh+54),radius=35,fill=(0,0,0,180))
        shadow=shadow.filter(ImageFilter.GaussianBlur(23))
        place(im,shadow,x-50,y-35,alpha)
        place(im,shot,x,y,alpha,width)

    def render_scene(self,index,t):
        theme=['warm','green','blue','blue','rose','green','warm'][index]
        im=background(theme).copy(); d=ImageDraw.Draw(im)
        place(im,self.brand,148,73)
        d.ellipse((120,84,129,93),fill='#9AC8B8')
        # A calm, slow field of tiny moving lights, kept away from the product UI.
        for k in range(12):
            x=100+((k*151+index*91)%1700);y=170+(k*113)%670+math.sin(t*.7+k)*7
            d.ellipse((x,y,x+2,y+2),fill=(154,177,207,55))
        enter=ease(t/.8); ox=32*(1-enter); a=enter
        top,main,sub=self.type[index]
        if index==0:
            place(im,top,158-ox,295,a);place(im,main,150-ox,330,a)
            place(im,sub,158-ox,505,a)
            d.line((160,605,300,605),fill=(205,176,145,140),width=2)
            place(im,label('Ruang kerja terasa lebih hidup.',23,'#8C99AD'),158,638,ease((t-.8)/.7))
            place(im,mascot('Greet',int(t*FPS)),1135+ox,230+math.sin(t)*3,a,width=430)
        elif index==1:
            place(im,top,158-ox,285,a);place(im,main,150-ox,335,a);place(im,sub,158-ox,580,a)
            d.rounded_rectangle((1000,290,1730,820),radius=35,outline=(126,149,180,55),width=2)
            d.line((1020,755,1710,755),fill=(156,175,195,75),width=2)
            x=1060+min(t/4,1)*240
            place(im,mascot('Walk',int(t*FPS)),x,410,a,width=245)
            place(im,label('Selalu ada di dekatmu.',21,'#8699AF'),1130,860,a)
        elif index==2:
            place(im,top,158-ox,275,a);place(im,main,150-ox,327,a);place(im,sub,158-ox,572,a)
            p=ease((t-.7)/.65); width=350+300*p
            if p<.03:
                place(im,self.shots['notch.png'],1325-width/2,300,a,width=width)
            else:
                self.card(im,'notch-carousel-music.png',1325-width/2,300,width,a*p)
            # Cursor and interaction ring are trailer annotations, outside captured UI.
            cx=1320+150*(1-ease(t/.6));cy=320+60*(1-ease(t/.6))
            if t<1.8:
                d=ImageDraw.Draw(im);r=14+20*max(0,math.sin((t-.6)*math.pi))
                d.ellipse((cx-r,cy-r,cx+r,cy+r),outline=(172,202,239,110),width=2)
                d.polygon([(cx,cy),(cx+7,cy+24),(cx+12,cy+16),(cx+23,cy+15)],fill='#F6F8FF')
        elif index==3:
            place(im,top,158-ox,238,a);place(im,main,150-ox,284,a);place(im,sub,158-ox,525,a)
            self.card(im,'notch-carousel-lyrics.png',920+ox,300,865,a)
            place(im,mascot('Singing',int(t*FPS)),430,657,a,width=205)
        elif index==4:
            place(im,top,158-ox,275,a);place(im,main,150-ox,325,a);place(im,sub,158-ox,570,a)
            self.card(im,'notch-carousel-focus.png',1100+ox,208,580,a)
        elif index==5:
            place(im,top,158-ox,255,a);place(im,main,150-ox,305,a);place(im,sub,158-ox,548,a)
            self.card(im,'notch-carousel-space.png',1155+ox,180,505,a)
            for j,word in enumerate(['Catatan','Tugas','Pintasan']):
                q=ease((t-.3-j*.18)/.6);x=160+j*180
                d=ImageDraw.Draw(im);d.rounded_rectangle((x,664+14*(1-q),x+154,720+14*(1-q)),radius=28,outline=(133,174,175,int(q*100)),width=1)
                place(im,label(word,22,'#C6D9D5'),x+25,674+14*(1-q),q)
        else:
            place(im,top,158-ox,250,a);place(im,main,150-ox,300,a);place(im,sub,158-ox,545,a)
            place(im,self.cta,160,645,ease((t-.35)/.6));place(im,self.url,160,747,ease((t-.5)/.6))
            place(im,mascot('Greet',int(t*FPS)),1210+ox,260,a,width=380)
        if index in (2,3,4,5): place(im,self.demo,148,998)
        else:place(im,self.footer,148,998)
        d=ImageDraw.Draw(im)
        for k in range(7):
            x=1580+k*29;d.rounded_rectangle((x,1007,x+(19 if k==index else 5),1011),radius=2,fill=('#D8C3AB' if k==index else '#374254'))
        return im.convert('RGB')

    def frame(self,t):
        index=next(i for i in range(7) if CUTS[i]<=t<CUTS[i+1])
        frame=self.render_scene(index,t-CUTS[index])
        if index and t-CUTS[index]<.26:
            previous=self.render_scene(index-1,t-CUTS[index-1])
            frame=Image.blend(previous,frame,ease((t-CUTS[index])/.26))
        fade=min(1,t/.55,(DURATION-t)/.6)
        if fade<1:frame=Image.blend(Image.new('RGB',(W,H),(8,12,20)),frame,max(0,fade))
        return frame


def main():
    p=argparse.ArgumentParser();p.add_argument('--assets');p.add_argument('--out',default=str(ROOT/'artifacts/trailer'));p.add_argument('--preview',action='store_true');args=p.parse_args()
    assets=prepare_assets(args.assets);out=Path(args.out);out.mkdir(parents=True,exist_ok=True)
    trailer=Trailer(assets)
    for t in [2.4,6.2,10.5,16,21.5,26,30]:trailer.frame(t).save(out/f'preview-{t:g}.jpg',quality=92)
    trailer.frame(2.4).save(out/'Momo-Trailer-Poster.png')
    if args.preview:return
    soundtrack(out/'soundtrack-original.wav')
    dest=out/'Momo-Trailer-1080p.mp4'
    cmd=['ffmpeg','-hide_banner','-loglevel','warning','-y','-f','rawvideo','-pixel_format','rgb24','-video_size',f'{W}x{H}','-framerate',str(FPS),'-i','pipe:0','-i',str(out/'soundtrack-original.wav'),'-c:v','libx264','-preset','fast','-crf','19','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-af','loudnorm=I=-18:TP=-1.5:LRA=9','-t',str(DURATION),'-movflags','+faststart','-metadata','title=Momo — Teman kecil di desktopmu','-metadata','comment=Original Momo vectors, native WPF UI screenshots with demo data, original instrumental soundtrack. Edited product presentation.',str(dest)]
    process=subprocess.Popen(cmd,stdin=subprocess.PIPE)
    try:
        for n in range(DURATION*FPS):
            process.stdin.write(trailer.frame(n/FPS).tobytes())
            if n%FPS==0:print(f'Render {n//FPS:02d}/{DURATION}s',flush=True)
    finally:process.stdin.close()
    if process.wait()!=0:raise RuntimeError('FFmpeg encoding failed')
    captions=[(0,4,'Kenalan dengan Momo. Teman kecil di desktopmu.'),(4,8,'Sedikit teman. Banyak suasana.'),(8,13.5,'Dekatkan kursor. Buka lebih banyak.'),(13.5,19,'Musik jalan. Momo ikut bernyanyi.'),(19,24,'Satu hal. Satu waktu.'),(24,28,'Ide tersimpan. Pikiran lebih ringan.'),(28,32,'Satu teman kecil. Banyak cerita. Momo untuk Windows.')]
    def stamp(t):
        ms=round(t*1000);return f'{ms//3600000:02d}:{ms//60000%60:02d}:{ms//1000%60:02d},{ms%1000:03d}'
    (out/'Momo-Trailer-ID.srt').write_text('\n\n'.join(f'{i+1}\n{stamp(a)} --> {stamp(b)}\n{text}' for i,(a,b,text) in enumerate(captions))+'\n',encoding='utf-8')
    with dest.open('rb') as f:digest=hashlib.file_digest(f,'sha256').hexdigest()
    (out/(dest.name+'.sha256')).write_text(digest+'  '+dest.name+'\n',encoding='ascii')
    (out/'trailer-info.json').write_text(json.dumps(dict(duration=DURATION,width=W,height=H,fps=FPS,native_ui_source=SOURCE_TAG,native_ui_sha256=SOURCE_SHA256,mp4_sha256=digest,visuals='Original Momo vectors and native Windows UI screenshots with authored fixture data; edited motion graphics.',audio='Original synthesized instrumental; no commercial samples or voice recording.'),indent=2)+'\n')
    print('Finished:',dest,dest.stat().st_size,'bytes',flush=True)


if __name__=='__main__':main()
