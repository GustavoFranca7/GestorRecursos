using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace GerenciadorClientes.Desktop.Controls;

// Imagem de pixel art ampliada sem borrar (cada pixel vira um quadrado nitido).
public class IconePixel : Image
{
    public IconePixel()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    // Os estilos escritos para Image continuam valendo para este controle.
    protected override Type StyleKeyOverride => typeof(Image);
}
