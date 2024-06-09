using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
namespace ReceiptStorage;

public interface IReceiptParser
{
    Task<ReceiptParserResponse> Parse(Content content, CancellationToken cancellationToken);
}
