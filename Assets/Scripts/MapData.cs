using System;
using UnityEngine;

/// <summary>
/// Holds dynamic map dimensions (width & height) for the active level.
/// Attached to MapBuilder_Workspace in each scene.
/// </summary>
public class MapData : MonoBehaviour
{
    public static MapData Instance { get; private set; }

    [Header("Map Perimeter Dimensions")]
    [Tooltip("Map width in meters (X axis: from 0 to mapWidth)")]
    public float mapWidth = 54.0f;

    [Tooltip("Map height in meters (Y axis: from 0 to mapHeight)")]
    public float mapHeight = 60.0f;

    private void Awake()
    {
        Instance = this;
        DetectDimensions();
    }

    private void OnValidate()
    {
        if (mapWidth < 10f) mapWidth = 10f;
        if (mapHeight < 10f) mapHeight = 10f;
    }

    public void SetDimensions(float width, float height)
    {
        mapWidth = Mathf.Max(10f, width);
        mapHeight = Mathf.Max(10f, height);
    }

    public void DetectDimensions()
    {
        // 1. Try reading size from AsphaltGround SpriteRenderer
        Transform groundTr = transform.Find("AsphaltGround");
        if (groundTr == null)
        {
            GameObject groundGo = GameObject.Find("AsphaltGround");
            if (groundGo != null) groundTr = groundGo.transform;
        }

        if (groundTr != null)
        {
            SpriteRenderer sr = groundTr.GetComponent<SpriteRenderer>();
            if (sr != null && sr.size.x > 5f && sr.size.y > 5f)
            {
                mapWidth = sr.size.x;
                mapHeight = sr.size.y;
                return;
            }
        }

        // 2. Try reading positions from Border_Top and Border_Right
        Transform borderTr = transform.Find("YardBorders");
        if (borderTr == null)
        {
            GameObject bGo = GameObject.Find("YardBorders");
            if (bGo != null) borderTr = bGo.transform;
        }

        if (borderTr != null)
        {
            Transform top = borderTr.Find("Border_Top");
            if (top != null && top.position.y > 5f)
            {
                mapHeight = top.position.y;
            }

            Transform right = borderTr.Find("Border_Right");
            if (right != null && right.position.x > 5f)
            {
                mapWidth = right.position.x;
            }
        }
    }
}
