using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
namespace Reinforcement
{
    internal static class FamilyCacheEvents
    {
        public static void DocumentChanged(object sender, DocumentChangedEventArgs args)
        {
            var doc = args.GetDocument();
            if (args.GetDeletedElementIds().Count > 0 || args.GetAddedElementIds().Concat(args.GetModifiedElementIds()).Any(id => (doc.GetElement(id) is ElementType || doc.GetElement(id) is Family)))
                HelperSeach.ClearCache(doc);
        }
        public static void DocumentClosing(object sender, DocumentClosingEventArgs args)
            => HelperSeach.ClearCache(args.Document);
    }
}
