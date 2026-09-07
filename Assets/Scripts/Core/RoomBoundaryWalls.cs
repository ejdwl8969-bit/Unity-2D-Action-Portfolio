using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-local, visual-free outer walls. Created only at runtime; no saved Scene
/// or existing gameplay collider is changed. Internal gaps remain untouched.
/// </summary>
public sealed class RoomBoundaryWalls : MonoBehaviour
{
    private const string CommittedRootName = "__CRG_COMMITTED__Gameplay";
    private const float WallThickness = 1f;
    // Well above the current double-jump range; not a ceiling or walkable surface.
    private const float VerticalPadding = 32f;
    private PhysicsMaterial2D wallMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        // Also safe when Enter Play Mode has domain reload disabled.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            EnsureWalls(SceneManager.GetSceneAt(i));
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureWalls(scene);
    }

    private static void EnsureWalls(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject[] roots = scene.GetRootGameObjects();
        RoomController room = null;
        PlayerController player = null;
        Transform committed = null;
        foreach (GameObject root in roots)
        {
            if (root.GetComponent<RoomBoundaryWalls>() != null)
                return;
            if (!root.activeInHierarchy || IsPreview(root.transform))
                continue;
            if (room == null) room = root.GetComponentInChildren<RoomController>();
            if (player == null) player = root.GetComponentInChildren<PlayerController>();
            if (root.name == CommittedRootName) committed = root.transform;
        }

        // Gameplay capability, not a hardcoded list of Room1/Room2 names.
        // Title has neither; WaitingRoom, combat rooms and boss rooms have both.
        if (room == null || player == null)
            return;

        Physics2D.SyncTransforms();
        var floors = new List<BoxCollider2D>();
        var surfaces = new List<BoxCollider2D>();
        if (committed != null)
        {
            Transform groundRoot = committed.Find("Geometry/GroundSegments");
            Transform geometryRoot = committed.Find("Geometry");
            if (groundRoot != null) Collect(groundRoot, floors);
            if (geometryRoot != null) Collect(geometryRoot, surfaces);
        }
        else
        {
            // Hand-authored WaitingRoom/BossRoom geometry uses the existing Ground tag.
            foreach (GameObject root in roots)
            {
                if (root.activeInHierarchy && !IsPreview(root.transform))
                    Collect(root.transform, floors);
            }
            surfaces.AddRange(floors);
        }

        if (!TryGetBounds(floors, out Bounds floorBounds))
        {
            Debug.LogWarning($"{scene.name}: Cannot create room boundary walls: no valid static Ground surfaces.", room);
            return;
        }

        Bounds verticalBounds = floorBounds;
        if (TryGetBounds(surfaces, out Bounds allSurfaces))
            verticalBounds.Encapsulate(allSurfaces);
        verticalBounds.Encapsulate(player.transform.position);

        var boundaryRoot = new GameObject("Room Boundaries (Runtime)");
        SceneManager.MoveGameObjectToScene(boundaryRoot, scene);
        RoomBoundaryWalls owner = boundaryRoot.AddComponent<RoomBoundaryWalls>();
        owner.wallMaterial = new PhysicsMaterial2D("Room Boundary - No Friction")
        {
            friction = 0f,
            bounciness = 0f
        };

        float bottom = verticalBounds.min.y - VerticalPadding;
        float top = verticalBounds.max.y + VerticalPadding;
        owner.CreateWall("Left Wall", floorBounds.min.x - WallThickness * 0.5f, bottom, top);
        owner.CreateWall("Right Wall", floorBounds.max.x + WallThickness * 0.5f, bottom, top);
        Physics2D.SyncTransforms();
    }

    private static void Collect(Transform root, List<BoxCollider2D> results)
    {
        foreach (BoxCollider2D collider in root.GetComponentsInChildren<BoxCollider2D>())
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger ||
                !collider.CompareTag("Ground") || IsPreview(collider.transform))
                continue;
            if (collider.attachedRigidbody != null && collider.attachedRigidbody.bodyType != RigidbodyType2D.Static)
                continue;
            Bounds bounds = collider.bounds;
            if (bounds.size.x <= bounds.size.y || bounds.size.y <= 0f || !IsFinite(bounds))
                continue;
            results.Add(collider);
        }
    }

    private static bool IsPreview(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (current.name.StartsWith("__CRG_PREVIEW__", StringComparison.Ordinal) ||
                current.name.StartsWith("__CRB_PREVIEW__", StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool TryGetBounds(List<BoxCollider2D> colliders, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (BoxCollider2D collider in colliders)
        {
            if (!found) { bounds = collider.bounds; found = true; }
            else bounds.Encapsulate(collider.bounds);
        }
        return found;
    }

    private static bool IsFinite(Bounds bounds)
    {
        return !float.IsNaN(bounds.min.x) && !float.IsInfinity(bounds.min.x) &&
            !float.IsNaN(bounds.max.x) && !float.IsInfinity(bounds.max.x) &&
            !float.IsNaN(bounds.min.y) && !float.IsInfinity(bounds.min.y) &&
            !float.IsNaN(bounds.max.y) && !float.IsInfinity(bounds.max.y);
    }

    private void CreateWall(string wallName, float centerX, float bottom, float top)
    {
        var wall = new GameObject(wallName);
        wall.transform.SetParent(transform, false);
        wall.transform.position = new Vector3(centerX, (bottom + top) * 0.5f, 0f);
        // Default layer collides with Player. Untagged is deliberate: Ground would
        // reset PlayerController's jump count on side contact. No Renderer is added.
        wall.layer = 0;
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.isTrigger = false;
        collider.size = new Vector2(WallThickness, top - bottom);
        collider.sharedMaterial = wallMaterial;
    }

    private void OnDestroy()
    {
        if (wallMaterial != null)
            Destroy(wallMaterial);
    }
}
