using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReceiptStorage.Templates;

public static class TemplateTools
{
    private static List<string> months = ["ЯНВАРЬ", "ФЕВРАЛЬ", "МАРТ", "АПРЕЛЬ", "МАЙ", "ИЮНЬ", "ИЮЛЬ", "АВГУСТ", "СЕНТЯБРЬ", "ОКТЯБРЬ", "НОЯБРЬ", "ДЕКАБРЬ"];

    public static string Normilize(string text) => text.ReplaceLineEndings("");

    public static int GetMonthNumber(string name) => months.IndexOf(name.ToUpperInvariant()) + 1;
}