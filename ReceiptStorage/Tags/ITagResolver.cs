using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReceiptStorage.Tags;

public interface ITagResolver
{
    ValueTask<string[]> ResolveTagsAsync(ReceiptDetails details, CancellationToken cancellationToken);
}