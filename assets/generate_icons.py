from PIL import Image, ImageDraw, ImageFilter
import math, os

SIZE=1024
img=Image.new('RGBA',(SIZE,SIZE),(0,0,0,0))
# rounded background with vertical/diagonal gradient
bg=Image.new('RGBA',(SIZE,SIZE),(0,0,0,0))
px=bg.load()
for y in range(SIZE):
    for x in range(SIZE):
        t=(x*0.35+y*0.65)/SIZE
        r=int(20*(1-t)+7*t)
        g=int(92*(1-t)+42*t)
        b=int(84*(1-t)+38*t)
        px[x,y]=(r,g,b,255)
mask=Image.new('L',(SIZE,SIZE),0)
d=ImageDraw.Draw(mask)
d.rounded_rectangle((0,0,SIZE-1,SIZE-1), radius=228, fill=255)
img.alpha_composite(Image.composite(bg,Image.new('RGBA',(SIZE,SIZE),(0,0,0,0)),mask), (0,0))
# glow
glow=Image.new('RGBA',(SIZE,SIZE),(0,0,0,0))
gd=ImageDraw.Draw(glow)
gd.ellipse((580,-260,1150,320), fill=(64,226,192,70))
glow=glow.filter(ImageFilter.GaussianBlur(90))
img.alpha_composite(Image.composite(glow,Image.new('RGBA',(SIZE,SIZE),(0,0,0,0)),mask),(0,0))
# deco rings
deco=Image.new('RGBA',(SIZE,SIZE),(0,0,0,0))
dd=ImageDraw.Draw(deco)
dd.arc((55,55,510,510), start=185, end=330, fill=(169,255,235,42), width=5)
dd.arc((575,590,1030,1040), start=5, end=150, fill=(169,255,235,34), width=5)
img.alpha_composite(Image.composite(deco,Image.new('RGBA',(SIZE,SIZE),(0,0,0,0)),mask),(0,0))

draw=ImageDraw.Draw(img)
mint=(112,246,218,255)
white=(246,255,252,255)
shadow=Image.new('RGBA',(SIZE,SIZE),(0,0,0,0))
sd=ImageDraw.Draw(shadow)

def arrow(d, y, x1, x2, color, head_at_right=True, width=86):
    # shaft
    d.line((x1,y,x2,y), fill=color, width=width)
    r=width//2
    d.ellipse((x1-r,y-r,x1+r,y+r), fill=color)
    d.ellipse((x2-r,y-r,x2+r,y+r), fill=color)
    h=92
    tipx=x2+(90 if head_at_right else -90)
    pts=((x2,y-h),(tipx,y),(x2,y+h))
    if head_at_right:
        d.polygon(pts, fill=color)
    else:
        d.polygon(pts, fill=color)

def draw_arrow(d, y, x1, x2, color, head_at_right=True):
    arrow(d, y, x1, x2, color, head_at_right, 86)

# soft shadow
draw_arrow(sd, 460, 300, 626, (0,0,0,110), True)
draw_arrow(sd, 596, 724, 398, (0,0,0,110), False)
shadow=shadow.filter(ImageFilter.GaussianBlur(24))
img.alpha_composite(shadow)
# arrows
draw_arrow(draw, 450, 292, 612, mint, True)
draw_arrow(draw, 588, 732, 412, white, False)
# central small dot (link light)
dot=Image.new('RGBA',(SIZE,SIZE),(0,0,0,0))
od=ImageDraw.Draw(dot)
od.ellipse((483,490,541,548), fill=(11,59,55,255))
od.ellipse((496,503,528,535), fill=(112,246,218,255))
img.alpha_composite(dot)
# save
os.makedirs('LinkDrop/assets', exist_ok=True)
img.save('LinkDrop/assets/linkdrop-icon-1024.png')
# foreground transparent for android
fg=Image.new('RGBA',(1024,1024),(0,0,0,0))
fd=ImageDraw.Draw(fg)
# scale arrows around center within safe zone (approx 66%)
# draw similar with no bg, scaled by .72 around center
art=Image.new('RGBA',(1024,1024),(0,0,0,0))
ad=ImageDraw.Draw(art)
arrow(ad, 450, 292, 612, mint, True, 86)
arrow(ad, 588, 732, 412, white, False, 86)
ad.ellipse((483,490,541,548), fill=(11,59,55,255))
ad.ellipse((496,503,528,535), fill=(112,246,218,255))
art=art.resize((740,740), Image.LANCZOS)
fg.alpha_composite(art,(142,142))
fg.save('LinkDrop/assets/linkdrop-foreground-1024.png')
# monochrome-ish dark glyph for small usage? use same
# windows ico
ico=img.resize((256,256), Image.LANCZOS)
ico.save('LinkDrop/assets/linkdrop.ico', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
# preview 256
img.resize((256,256), Image.LANCZOS).save('LinkDrop/assets/linkdrop-preview.png')
print('generated')
