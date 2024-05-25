using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace ReceiptStorage;

public interface IReceiptStorageHandler
{
    Task<ReceiptHandleResponse> Handle(Content content, IUser user, CancellationToken cancellationToken);
}

