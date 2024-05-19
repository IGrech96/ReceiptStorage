using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReceiptStorage;

public interface IReceiptStorage
{
    Task SaveAsync(Stream content, ReceiptDetails info, string name, CancellationToken cancellationToken);
}