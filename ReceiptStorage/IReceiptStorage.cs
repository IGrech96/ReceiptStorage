﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReceiptStorage;

public interface IReceiptStorage
{
    Task SaveAsync(Content content, ReceiptDetails info, IUser user, CancellationToken cancellationToken);
}