from pathlib import Path
import argparse
import cv2
from PIL import Image, ImageDraw, ImageFont

parser = argparse.ArgumentParser()
parser.add_argument('--start', type=float, default=0)
parser.add_argument('--end', type=float, default=20.2)
parser.add_argument('--step', type=float, default=.5)
parser.add_argument('--name', default='Overview')
parser.add_argument('--width', type=int, default=320)
parser.add_argument('--cols', type=int, default=5)
args = parser.parse_args()
folder = Path(__file__).parent
video = cv2.VideoCapture(r'E:\Drive_E_Download\Gacha_pull.mp4')
fps = video.get(cv2.CAP_PROP_FPS)
count = int(video.get(cv2.CAP_PROP_FRAME_COUNT))
width = args.width
height = round(width * video.get(cv2.CAP_PROP_FRAME_HEIGHT) / video.get(cv2.CAP_PROP_FRAME_WIDTH))
images = []
times = []
for sample in range(round((args.end-args.start)/args.step)+1):
    frame_index = round((args.start + sample*args.step) * fps)
    if frame_index >= count: break
    video.set(cv2.CAP_PROP_POS_FRAMES, frame_index)
    okay, frame = video.read()
    if not okay: continue
    image = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
    time = frame_index/fps
    image.save(folder / f'{args.name}-{frame_index:04d}-{time:.3f}s.jpg', quality=94)
    images.append(image.resize((width,height), Image.Resampling.LANCZOS))
    times.append((frame_index,time))
video.release()
cell_height = height+24
sheet = Image.new('RGB',(args.cols*width, ((len(images)+args.cols-1)//args.cols)*cell_height),(15,18,24))
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype(r'C:\Windows\Fonts\consola.ttf',16)
for i,(image,(frame,time)) in enumerate(zip(images,times)):
    x = (i%args.cols)*width
    y = (i//args.cols)*cell_height
    sheet.paste(image,(x,y))
    draw.text((x+5,y+height+2),f'{time:.3f}s / f{frame}',font=font,fill='white')
path = folder / f'{args.name}-contact.jpg'
sheet.save(path,quality=94)
print(str(path))
