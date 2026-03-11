using HarmonyLib;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;

namespace EditorPlus.Patches
{
    /// <summary>
    /// Disables camera collision to allow truly free camera movement through structures
    /// Simple approach: just disable collision components and set camera to no-collision layer
    /// </summary>
    internal static class FreeCameraCollisionPatch
    {
        /// <summary>
        /// MonoBehaviour to disable camera collision components
        /// </summary>
        internal class FreeCameraMonitor : MonoBehaviour
        {
            private Camera _camera;
            private bool _hasInitialized = false;

            private void Start()
            {
                Plugin.Logger?.LogInfo("[EditorPlus] FreeCameraMonitor.Start() called");
                StartCoroutine(InitializeCameraControlDelayed());
            }

            private IEnumerator InitializeCameraControlDelayed()
            {
                // Wait a few frames to ensure the camera is created
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                
                InitializeCameraControl();
            }

            private void InitializeCameraControl()
            {
                // Try multiple ways to find the camera
                _camera = Camera.main;
                if (_camera == null)
                {
                    var allCameras = FindObjectsOfType<Camera>();
                    if (allCameras != null && allCameras.Length > 0)
                    {
                        _camera = allCameras.FirstOrDefault(c => c != null && c.gameObject.activeInHierarchy && c.enabled);
                        if (_camera == null)
                        {
                            _camera = allCameras[0];
                        }
                    }
                }
                
                if (_camera == null)
                {
                    Plugin.Logger?.LogWarning("[EditorPlus] Camera not found, will retry in Update");
                    return;
                }

                Plugin.Logger?.LogInfo($"[EditorPlus] Found camera: {_camera.name}");

                // Disable all collision components
                DisableCameraCollisionComponents();
                
                // Set camera to ignore raycast layer (layer 2)
                SetCameraToNoCollisionLayer();

                _hasInitialized = true;
                Plugin.Logger?.LogInfo($"[EditorPlus] Free camera collision disabled! Camera: {_camera.name}");
            }

            private void Update()
            {
                if (!_hasInitialized)
                {
                    InitializeCameraControl();
                    return;
                }

                if (_camera == null)
                {
                    _camera = Camera.main;
                    if (_camera == null) return;
                    InitializeCameraControl();
                    return;
                }

                // Continuously disable collision components (in case they get re-enabled)
                DisableCameraCollisionComponents();
                SetCameraToNoCollisionLayer();
            }

            /// <summary>
            /// Put camera on a layer that doesn't collide with anything
            /// </summary>
            private void SetCameraToNoCollisionLayer()
            {
                if (_camera == null) return;

                // Use layer 2 (Ignore Raycast) - this layer typically doesn't collide with anything
                int noCollisionLayer = 2;
                
                if (_camera.gameObject.layer != noCollisionLayer)
                {
                    _camera.gameObject.layer = noCollisionLayer;
                }

                // Also set parent to no-collision layer if it exists
                if (_camera.transform.parent != null && _camera.transform.parent.gameObject.layer != noCollisionLayer)
                {
                    _camera.transform.parent.gameObject.layer = noCollisionLayer;
                }
            }

            private void DisableCameraCollisionComponents()
            {
                if (_camera == null) return;
                
                // Disable CharacterController
                var controller = _camera.GetComponent<CharacterController>();
                if (controller != null && controller.enabled)
                {
                    controller.enabled = false;
                }

                // Disable Rigidbody if present
                var rigidbody = _camera.GetComponent<Rigidbody>();
                if (rigidbody != null)
                {
                    if (!rigidbody.isKinematic)
                    {
                        rigidbody.isKinematic = true;
                    }
                    rigidbody.useGravity = false;
                    rigidbody.constraints = RigidbodyConstraints.FreezeAll;
                }

                // Disable all colliders on camera
                var colliders = _camera.GetComponents<Collider>();
                foreach (var col in colliders)
                {
                    if (col != null && col.enabled)
                    {
                        col.enabled = false;
                    }
                }

                // Also check parent objects for collision components
                var parent = _camera.transform.parent;
                if (parent != null)
                {
                    var parentController = parent.GetComponent<CharacterController>();
                    if (parentController != null && parentController.enabled)
                    {
                        parentController.enabled = false;
                    }

                    var parentRigidbody = parent.GetComponent<Rigidbody>();
                    if (parentRigidbody != null && !parentRigidbody.isKinematic)
                    {
                        parentRigidbody.isKinematic = true;
                        parentRigidbody.useGravity = false;
                    }

                    var parentColliders = parent.GetComponents<Collider>();
                    foreach (var col in parentColliders)
                    {
                        if (col != null && col.enabled)
                        {
                            col.enabled = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initialize the free camera monitor
        /// </summary>
        public static void Initialize()
        {
            Plugin.Logger?.LogInfo("[EditorPlus] FreeCameraCollisionPatch.Initialize() called");
            
            // Try to find existing monitor
            var existing = GameObject.Find("[EditorPlus_FreeCameraMonitor]");
            if (existing != null)
            {
                Plugin.Logger?.LogInfo("[EditorPlus] FreeCameraMonitor GameObject already exists");
                return;
            }
            
            try
            {
                var go = new GameObject("[EditorPlus_FreeCameraMonitor]");
                var monitor = go.AddComponent<FreeCameraMonitor>();
                UnityEngine.Object.DontDestroyOnLoad(go);
                Plugin.Logger?.LogInfo("[EditorPlus] Free camera collision monitor GameObject created");
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"[EditorPlus] Failed to create FreeCameraMonitor: {ex}");
            }
        }

        /// <summary>
        /// Patch Physics.CheckSphere to always return false (no collision) when checking camera position
        /// This allows the camera to move through objects
        /// </summary>
        [HarmonyPatch(typeof(Physics), nameof(Physics.CheckSphere), new[] { typeof(Vector3), typeof(float), typeof(int) })]
        static class PhysicsCheckSpherePatch
        {
            static bool Prefix(Vector3 position, float radius, int layerMask, ref bool __result)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    float distance = Vector3.Distance(position, cam.transform.position);
                    // Block collision checks near camera
                    if (radius < 10f && distance < 20f)
                    {
                        __result = false; // No collision - allow camera through
                        return false; // Skip original method
                    }
                }
                return true; // Use original for other checks
            }
        }

        /// <summary>
        /// Patch Physics.CheckCapsule to allow camera through
        /// </summary>
        [HarmonyPatch(typeof(Physics), nameof(Physics.CheckCapsule), new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(int) })]
        static class PhysicsCheckCapsulePatch
        {
            static bool Prefix(Vector3 point1, Vector3 point2, float radius, int layerMask, ref bool __result)
            {
                var cam = Camera.main;
                if (cam != null && radius < 10f)
                {
                    var midPoint = (point1 + point2) / 2f;
                    if (Vector3.Distance(midPoint, cam.transform.position) < 20f)
                    {
                        __result = false; // No collision
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// Patch Physics.Raycast to ignore collisions when ray is from camera
        /// </summary>
        [HarmonyPatch(typeof(Physics), nameof(Physics.Raycast), new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(int) })]
        static class PhysicsRaycastPatch
        {
            static bool Prefix(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, ref bool __result)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    float distanceFromCam = Vector3.Distance(origin, cam.transform.position);
                    if (distanceFromCam < 5f)
                    {
                        __result = false;
                        return false; // Skip original method
                    }
                }
                return true; // Use original for other checks
            }
        }

        /// <summary>
        /// Patch Physics.RaycastNonAlloc to ignore collisions when ray is from camera
        /// </summary>
        [HarmonyPatch(typeof(Physics), nameof(Physics.RaycastNonAlloc), new[] { typeof(Vector3), typeof(Vector3), typeof(RaycastHit[]), typeof(float), typeof(int) })]
        static class PhysicsRaycastNonAllocPatch
        {
            static bool Prefix(Vector3 origin, Vector3 direction, RaycastHit[] results, float maxDistance, int layerMask, ref int __result)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    float distanceFromCam = Vector3.Distance(origin, cam.transform.position);
                    if (distanceFromCam < 5f)
                    {
                        __result = 0;
                        return false; // Skip original method
                    }
                }
                return true; // Use original for other checks
            }
        }

        /// <summary>
        /// Patch Physics.RaycastAll to ignore collisions when rays are from camera
        /// </summary>
        [HarmonyPatch(typeof(Physics), nameof(Physics.RaycastAll), new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(int) })]
        static class PhysicsRaycastAllPatch
        {
            static bool Prefix(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, ref RaycastHit[] __result)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    float distanceFromCam = Vector3.Distance(origin, cam.transform.position);
                    if (distanceFromCam < 5f)
                    {
                        __result = new RaycastHit[0];
                        return false; // Skip original method
                    }
                }
                return true; // Use original for other checks
            }
        }

        /// <summary>
        /// Patch CharacterController.Move to allow free camera movement
        /// </summary>
        [HarmonyPatch(typeof(CharacterController), nameof(CharacterController.Move), new[] { typeof(Vector3) })]
        static class CharacterControllerMovePatch
        {
            static bool Prefix(CharacterController __instance, Vector3 motion, ref CollisionFlags __result)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    if (__instance.transform == cam.transform || __instance.transform == cam.transform.parent)
                    {
                        // Allow the move to proceed without collision checks
                        __instance.transform.position += motion;
                        __result = CollisionFlags.None;
                        return false; // Skip original Move method
                    }
                }
                return true; // Use original for other CharacterControllers
            }
        }
    }
}
