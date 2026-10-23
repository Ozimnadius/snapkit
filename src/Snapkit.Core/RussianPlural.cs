namespace Snapkit.Core;

/// <summary>Число со словом в нужной форме: «1 изображение», «2 изображения», «5 изображений».</summary>
public static class RussianPlural
{
    public static string Format(int n, string one, string few, string many)
    {
        var abs = Math.Abs(n);
        var lastTwo = abs % 100;
        var last = abs % 10;
        var form = lastTwo is >= 11 and <= 14 ? many
            : last == 1 ? one
            : last is >= 2 and <= 4 ? few
            : many;
        return $"{n} {form}";
    }
}
