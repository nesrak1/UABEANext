using Dock.Model.Mvvm.Controls;
using System.Collections.Generic;

namespace UABEANext4.Logic.Documents;
// this is temporary until this gets mvvm'd
public class DocumentManager
{
    public List<Document> Documents = [];
    public Document? LastFocusedDocument = null;

    public void Clear()
    {
        Documents = [];
        LastFocusedDocument = null;
    }
}
