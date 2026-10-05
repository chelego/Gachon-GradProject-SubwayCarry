"""Assemble the generated inspection views into temporary contact sheets, not game textures."""
from PIL import Image, ImageDraw
from pathlib import Path
root=Path(__file__).resolve().parent.parent
folder=root.parents[3]/'Temp'/'ThreeDRebuild'/'KitReview'
names=sorted(p.name[:-len('_Material.png')] for p in folder.glob('*_Material.png'))
views=['Front','Back','Left','Right','Top','Material','Clay','Contact']
for group in range(0,len(names),5):
    current=names[group:group+5]; canvas=Image.new('RGB',(2560,len(current)*285),(35,40,45)); draw=ImageDraw.Draw(canvas)
    for row,name in enumerate(current):
        draw.text((8,row*285+4),name,fill='white')
        for col,view in enumerate(views):
            canvas.paste(Image.open(folder/(name+'_'+view+'.png')).convert('RGB'),(col*320,row*285+25))
            draw.text((col*320+5,row*285+27),view,fill='white')
    canvas.save(folder/('Assets_%02d.png'%(group//5)))
print('Inspection sheets',len(names),'assets',len(names)*len(views),'views',folder)
