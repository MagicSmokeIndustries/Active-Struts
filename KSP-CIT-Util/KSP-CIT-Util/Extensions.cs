using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CIT_Util
{
    public static class Extensions
    {
        public static double GetMassOfPartAndChildren(this Part part)
        {
            if (part == null)
            {
                return 0d;
            }
            double sum = part.mass;
            sum += part.children.Sum(pc => pc.GetMassOfPartAndChildren());
            return sum;
        }

        public static void SetActive(this BaseEvent e, bool unfocused = false)
        {
            e.active = e.guiActive = true;
            e.guiActiveUnfocused = unfocused;
        }

        public static void SetInactive(this BaseEvent e, bool unfocused = false)
        {
            e.active = e.guiActive = false;
            e.guiActiveUnfocused = unfocused;
        }

        public static List<PartResource> GetResources(this Part p)
        {
            return (from object resource in p.Resources
                    select resource as PartResource).ToList();
        }

        public static float[] GetRgba(this Color color)
        {
            var ret = new float[4];
            ret[0] = color.r;
            ret[1] = color.g;
            ret[2] = color.b;
            ret[3] = color.a;
            return ret;
        }

        public static bool IsMouseOverRect(this Rect windowRect)
        {
            var mousePosFromEvent = Event.current.mousePosition;
            return windowRect.Contains(mousePosFromEvent);
        }

        public static Color MakeColorTransparent(this Color color, float transparency)
        {
            var rgba = color.GetRgba();
            return new Color(rgba[0], rgba[1], rgba[2], transparency);
        }

        public static Part PartFromHit(this RaycastHit hit)
        {
            if (hit.collider == null || hit.collider.gameObject == null)
            {
                return null;
            }
            var go = hit.collider.gameObject;
            var p = Part.FromGO(go);
            while (p == null)
            {
                if (go.transform != null && go.transform.parent != null && go.transform.parent.gameObject != null)
                {
                    go = go.transform.parent.gameObject;
                }
                else
                {
                    break;
                }
                p = Part.FromGO(go);
            }
            return p;
        }

        public static void RecursePartList(this Part part, ICollection<Part> list)
        {
            list.Add(part);
            foreach (var p in part.children)
            {
                p.RecursePartList(list);
            }
        }

        public static bool TryGetModule<T>(this Part part, out T outModule) where T : PartModule
        {
            outModule = part.FindModuleImplementing<T>();
            return outModule != null;
        }

        private static IEnumerable<Part> FindAllFuelLineConnectedSourceParts(this Part refPart, List<Part> allParts, bool outRes)
        {
            return allParts.OfType<FuelLine>()
                           .Where(fl => fl.target != null && fl.parent != null && outRes ? fl.parent == refPart : fl.target == refPart)
                           .Select(fl => outRes ? fl.target : fl.parent);
        }

        public static List<Part> FindPartsInSameResStack(this Part refPart, List<Part> allParts, HashSet<Part> searchedParts, bool outRes, bool isFirst = false)
        {
            var partList = new List<Part> {refPart};
            searchedParts.Add(refPart);
            foreach (var attachNode in refPart.attachNodes.Where(an => an.attachedPart != null && !searchedParts.Contains(an.attachedPart) && an.attachedPart.fuelCrossFeed && an.nodeType == AttachNode.NodeType.Stack))
            {
                partList.AddRange(attachNode.attachedPart.FindPartsInSameResStack(allParts, searchedParts, outRes));
            }
            foreach (var fuelLinePart in refPart.FindAllFuelLineConnectedSourceParts(allParts, outRes).Where(flp => !searchedParts.Contains(flp)))
            {
                partList.AddRange(fuelLinePart.FindPartsInSameResStack(allParts, searchedParts, outRes));
            }
            if (isFirst && refPart.attachMode == AttachModes.SRF_ATTACH)
            {
                partList.AddRange(refPart.srfAttachNode.attachedPart.FindPartsInSameResStack(allParts, searchedParts, outRes));
            }
            return partList;
        }

        public static List<Part> FindPartsInSameStage(this Part refPart, List<Part> allParts, bool outRes)
        {
            var partList = allParts.Where(vPart => vPart.inverseStage == refPart.inverseStage).ToList();
            partList.AddRange(refPart.FindAllFuelLineConnectedSourceParts(allParts, outRes));
            return partList;
        }

        public static bool In<T>(this T instance, IEnumerable<T> set)
        {
            if (set == null)
            {
                throw new ArgumentNullException("set");
            }
            return set.Contains(instance);
        }
    }
}