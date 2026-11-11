using System;
using System.Collections.Generic;
using System.Drawing;

namespace UlanziLite {
    sealed class AppItem {
        public readonly string Path;
        public AppItem(string path) {Path=path;}
        public override string ToString() {return System.IO.Path.GetFileNameWithoutExtension(Path);}
    }

    sealed class AppComboBox : ThemeComboBox {
        readonly Dictionary<string,Icon> icons=new Dictionary<string,Icon>(StringComparer.OrdinalIgnoreCase);

        public override void DrawContent(Graphics graphics,Rectangle bounds,int index,Color color) {
            var item=index<0?null:Items[index] as AppItem;
            if(item!=null) {
                Icon icon;
                if(!icons.TryGetValue(item.Path,out icon)) {
                    // Resolve each shortcut only once, when it first becomes visible.
                    try {icon=ApplicationIcons.Load(item.Path);}catch {icon=null;}
                    icons[item.Path]=icon;
                }
                int size=Math.Min(bounds.Height-4,Math.Max(16,(int)Math.Round(24*graphics.DpiX/96)));
                if(size>0)graphics.DrawIcon(icon??SystemIcons.Application,new Rectangle(bounds.Left+4,bounds.Top+(bounds.Height-size)/2,size,size));
                int inset=Math.Max(0,size)+10;
                bounds=new Rectangle(bounds.Left+inset,bounds.Top,Math.Max(0,bounds.Width-inset),bounds.Height);
            }
            base.DrawContent(graphics,bounds,index,color);
        }

        protected override void Dispose(bool disposing) {
            if(disposing) {foreach(var icon in icons.Values)if(icon!=null)icon.Dispose();icons.Clear();}
            base.Dispose(disposing);
        }
    }
}
