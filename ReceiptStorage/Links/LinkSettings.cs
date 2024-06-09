using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReceiptStorage.Links;

public class LinkSettings
{
    public Dictionary<string, Dictionary<string, string>> Rules { get; set; } = new();
}
