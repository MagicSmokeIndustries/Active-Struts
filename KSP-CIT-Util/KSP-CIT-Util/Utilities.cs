using System;
using System.Collections.Generic;
using System.Linq;
using CIT_Util.Types;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CIT_Util
{
    public static class Utilities
    {
        public static GameObject CreatePrimitive(PrimitiveType type, Color color, Vector3 scale, bool isActive, bool hasCollider, bool hasRigidbody, bool hasRenderer = true, string name = "", string shader = "Diffuse")
        {
            var go = GameObject.CreatePrimitive(type);
            if (string.IsNullOrEmpty(name))
            {
                name = Guid.NewGuid().ToString();
            }
            go.name = name;
            if (hasCollider)
            {
                if (go.collider == null)
                {
                    go.AddComponent<MeshCollider>();
                }
            }
            else
            {
                Object.DestroyImmediate(go.collider);
            }
            go.transform.localScale = scale;
            if (hasRigidbody)
            {
                if (go.rigidbody == null)
                {
                    go.AddComponent<Rigidbody>();
                }
            }
            else
            {
                Object.DestroyImmediate(go.rigidbody);
            }
            var mr = go.GetComponent<MeshRenderer>();
            if (hasRenderer)
            {
                mr.name = name;
                mr.material = new Material(Shader.Find(shader)) {color = color};
            }
            else
            {
                Object.DestroyImmediate(mr);
            }
            go.SetActive(isActive);
            return go;
        }

        public static bool EditorAboutToAttach(bool movingToo = false)
        {
            return HighLogic.LoadedSceneIsEditor &&
                   EditorLogic.SelectedPart != null &&
                   EditorLogic.SelectedPart.potentialParent != null;
        }

        public static List<T> GetAllModulesInLoadRange<T>(string moduleName, Func<PartModule, T> convFunc) where T : class
        {
            var moduleList = new List<T>();
            if (!HighLogic.LoadedSceneIsFlight)
            {
                if (HighLogic.LoadedSceneIsEditor)
                {
                    moduleList = EditorLogic.fetch.ship.Parts.Where(part => part.Modules.Contains(moduleName))
                                            .Select(part => convFunc(part.Modules[moduleName]))
                                            .ToList();
                }
                return moduleList;
            }
            moduleList = FlightGlobals.Vessels.Where(v => v.loaded)
                                      .Where(v => v.Parts.Any(p => p.Modules.Contains(moduleName)))
                                      .SelectMany(v => v.Parts)
                                      .Where(part => part.Modules.Contains(moduleName))
                                      .Select(part => convFunc(part.Modules[moduleName]))
                                      .ToList();
            return moduleList;
        }

        public static List<Part> GetAllModulesInLoadRange(Func<Part, bool> filterFunc = null)
        {
            var cond = filterFunc ?? (p => true);
            if (HighLogic.LoadedSceneIsFlight)
            {
                return FlightGlobals.Vessels.Where(v => v.loaded)
                                    .Where(v => v.Parts.Any(cond))
                                    .SelectMany(v => v.Parts)
                                    .ToList();
            }
            if (HighLogic.LoadedSceneIsEditor)
            {
                return EditorLogic.fetch.ship.Parts.Where(cond)
                                  .Select(part => part)
                                  .ToList();
            }
            return new List<Part>();
        }

        public static Color GetColorFromRgb(byte r, byte g, byte b, byte a = 255)
        {
            const float factor = 255f;
            return new Color(r/factor, g/factor, b/factor, a/factor);
        }

        public static Tuple<Vector3, RaycastHit?> GetMouseWorldPosition(float rayDistance)
        {
            var ray = HighLogic.LoadedSceneIsFlight ? FlightCamera.fetch.mainCamera.ScreenPointToRay(Input.mousePosition) : Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            return Physics.Raycast(ray, out hit, rayDistance) ? new Tuple<Vector3, RaycastHit?>(hit.point, hit) : new Tuple<Vector3, RaycastHit?>(Vector3.zero, null);
        }

        public static List<Part> ListEditorParts(bool includeSelected)
        {
            var el = EditorLogic.fetch;
            var list = el.getSortedShipList();
            if (!includeSelected || !EditorAboutToAttach())
            {
                return list;
            }
            EditorLogic.SelectedPart.RecursePartList(list);
            foreach (var sym in EditorLogic.SelectedPart.symmetryCounterparts)
            {
                sym.RecursePartList(list);
            }
            return list;
        }

        public static RaycastResult PerformRaycast(Vector3 origin, Vector3 target, Vector3 originUp, float rayDistance, Part partToIgnore = null)
        {
            var arr = partToIgnore != null ? new[] {partToIgnore} : new Part[0];
            return PerformRaycast(origin, target, originUp, rayDistance, arr);
        }

        public static RaycastResult PerformRaycast(Vector3 origin, Vector3 target, Vector3 originUp, float rayDistance, Vessel vesselToIgnore = null)
        {
            var arr = vesselToIgnore != null ? vesselToIgnore.Parts.ToArray() : new Part[0];
            return PerformRaycast(origin, target, originUp, rayDistance, arr);
        }

        public static RaycastResult PerformRaycast(Vector3 origin, Vector3 target, Vector3 originUp, float rayDistance, ICollection<Part> partsToIgnore)
        {
            var dir = (target - origin).normalized;
            return PerformRaycastIntoDirection(origin, dir, originUp, rayDistance, partsToIgnore);
        }

        public static RaycastResult PerformRaycastIntoDirection(Vector3 origin, Vector3 direction, Vector3 originUp, float rayDistance, ICollection<Part> partsToIgnore)
        {
            var ray = new Ray(origin, direction);
            var hits = Physics.RaycastAll(ray, rayDistance);
            var validHits = new List<Tuple<RaycastHit, Part>>(hits.Length);
            validHits.AddRange(from raycastHit in hits
                               let part = raycastHit.PartFromHit()
                               where part != null
                               where !partsToIgnore.Contains(part)
                               orderby raycastHit.distance ascending
                               select new Tuple<RaycastHit, Part>(raycastHit, part));
            var nearestHit = validHits.FirstOrDefault();
            var rayRes = new RaycastResult {HitResult = false};
            if (nearestHit == null)
            {
                return rayRes;
            }
            var hit = nearestHit.Item1;
            rayRes.HittedPart = nearestHit.Item2;
            rayRes.DistanceFromOrigin = hit.distance;
            rayRes.Hit = hit;
            rayRes.HitResult = true;
            rayRes.Ray = ray;
            rayRes.RayAngle = Vector3.Angle(direction, originUp);
            return rayRes;
        }
    }
}