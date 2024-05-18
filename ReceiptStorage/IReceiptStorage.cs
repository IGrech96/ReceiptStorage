using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReceiptStorage;

public interface IReceiptStorage
{
    Task<ReceiptHandleResponse> Handle(Stream content, string name);
}