namespace RAM.Core.Models;

public readonly record struct WindowPlacement(int X, int Y, int Width, int Height)
{
    public bool IsValid => Width > 0 && Height > 0;
}
