using System.Collections.Generic;

namespace ActiveStruts.Util
{
    public static class PartExtensions
    {
        public static bool TryGetModule<T>(this Part part, out T outModule) where T : PartModule
        {
            outModule = part.FindModuleImplementing<T>();
            return outModule != null;
        }

        public static void RecursePartList(this Part part, ICollection<Part> list)
        {
            list.Add(part);
            foreach (var p in part.children)
            {
                p.RecursePartList(list);
            }
        }

    }
}
