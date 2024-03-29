﻿using UnityEngine;

public class HexFeatureManager : MonoBehaviour {

    public HexFeatureCollection[] urbanCollections, farmCollections, plantCollections;//здания, растения и фермы

    public HexMesh walls;//стены
    public Transform wallTower, bridge;//мосты и башни
    public Transform[] special;//мегаструктуры

    Transform container;//контейнер под всё, что есть из объектов на сцене(для переотрисовки)

    public void Clear()
    {
        if (container)
        {
            Destroy(container.gameObject);
        }
        container = new GameObject("Features Container").transform;
        container.SetParent(transform, false);
        walls.Clear();//переотрисовка стен
    }

    public void Apply()
    {
        walls.Apply();//переотрисовка стен
    }

    public void AddFeature(HexCell cell, Vector3 position)
    {
        if (cell.IsSpecial)
        {
            return;
        }
        //значение хэш-сетки для конкретного объекта
        HexHash hash = HexMetrics.SampleHashGrid(position);
        //домики
        Transform prefab = PickPrefab(
            urbanCollections, cell.UrbanLevel, hash.a, hash.d
        );
        //фермы
        Transform otherPrefab = PickPrefab(
            farmCollections, cell.FarmLevel, hash.b, hash.d
        );
        float usedHash = hash.a;
        if (prefab)//выбор, что заспавнить, если существуют 2 префаба или больше(спавн того, чей хэш меньше)
        {
            if (otherPrefab && hash.b < hash.a)
            {
                prefab = otherPrefab;
                usedHash = hash.b;
            }
        }
        else if (otherPrefab)
        {
            prefab = otherPrefab;
            usedHash = hash.b;
        }
        //растения
        otherPrefab = PickPrefab(
            plantCollections, cell.PlantLevel, hash.c, hash.d
        );
        if (prefab)
        {
            if (otherPrefab && hash.c < usedHash)//сравнение с хэшом объекта который ранее был предпочтительным
            {
                prefab = otherPrefab;
            }
        }
        else if (otherPrefab)
        {
            prefab = otherPrefab;
        }
        else
        {
            return;
        }
        Transform instance = Instantiate(prefab);
        position.y += instance.localScale.y * 0.5f;
        instance.localPosition = HexMetrics.Perturb(position);
        instance.localRotation = Quaternion.Euler(0f, 360f * hash.e, 0f);
        //ставим контейнер родителем, чтобы удалять объекты каждый раз, когда обновляется чанк
        //и спавнить новый с привязкой к старому контейнеру
        instance.SetParent(container, false);
    }
    /// <summary>
    /// Для создания основных участков стены
    /// </summary>
    /// <param name="near"></param>
    /// <param name="nearCell"></param>
    /// <param name="far"></param>
    /// <param name="farCell"></param>
    public void AddWall(
    EdgeVertices near, HexCell nearCell,
    EdgeVertices far, HexCell farCell,
    bool hasRiver, bool hasRoad
        )
    {
        if (nearCell.Walled != farCell.Walled &&
            !nearCell.IsUnderwater && !farCell.IsUnderwater &&
            nearCell.GetEdgeType(farCell) != HexEdgeType.Cliff)
        {
            AddWallSegment(near.v1, far.v1, near.v2, far.v2);
            if (hasRiver || hasRoad)//если есть дороги или реки, то оставляем место
            {
                AddWallCap(near.v2, far.v2);
                AddWallCap(far.v4, near.v4);
            }
            else
            {
                AddWallSegment(near.v2, far.v2, near.v3, far.v3);
                AddWallSegment(near.v3, far.v3, near.v4, far.v4);
            }
            AddWallSegment(near.v4, far.v4, near.v5, far.v5);
        }
    }
    /// <summary>
    /// Для создания углов
    /// </summary>
    /// <param name="c1"></param>
    /// <param name="cell1"></param>
    /// <param name="c2"></param>
    /// <param name="cell2"></param>
    /// <param name="c3"></param>
    /// <param name="cell3"></param>
    public void AddWall(
        Vector3 c1, HexCell cell1,
        Vector3 c2, HexCell cell2,
        Vector3 c3, HexCell cell3
    )
    {
        if (cell1.Walled)//условие для выбора нужной клетки, как нижней (нижняя(pivot), левая, правая)
        {
            if (cell2.Walled)
            {
                if (!cell3.Walled)
                {
                    AddWallSegment(c3, cell3, c1, cell1, c2, cell2);
                }
            }
            else if (cell3.Walled)
            {
                AddWallSegment(c2, cell2, c3, cell3, c1, cell1);
            }
            else
            {
                AddWallSegment(c1, cell1, c2, cell2, c3, cell3);
            }
        }
        else if (cell2.Walled)
        {
            if (cell3.Walled)
            {
                AddWallSegment(c1, cell1, c2, cell2, c3, cell3);
            }
            else
            {
                AddWallSegment(c2, cell2, c3, cell3, c1, cell1);
            }
        }
        else if (cell3.Walled)
        {
            AddWallSegment(c3, cell3, c1, cell1, c2, cell2);
        }
    }
    /// <summary>
    /// Для создания углов
    /// </summary>
    /// <param name="pivot"></param>
    /// <param name="pivotCell"></param>
    /// <param name="left"></param>
    /// <param name="leftCell"></param>
    /// <param name="right"></param>
    /// <param name="rightCell"></param>
    private void AddWallSegment(
        Vector3 pivot, HexCell pivotCell,//pivot - клетка вокруг строятся стены
        Vector3 left, HexCell leftCell,
        Vector3 right, HexCell rightCell
    )
    {
        if (pivotCell.IsUnderwater)
        {
            return;
        }

        bool hasLeftWall = !leftCell.IsUnderwater &&
            pivotCell.GetEdgeType(leftCell) != HexEdgeType.Cliff;
        bool hasRighWall = !rightCell.IsUnderwater &&
            pivotCell.GetEdgeType(rightCell) != HexEdgeType.Cliff;

        if (hasLeftWall)
        {
            if (hasRighWall)
            {
                bool hasTower = false;
                if (leftCell.Elevation == rightCell.Elevation)
                {
                    HexHash hash = HexMetrics.SampleHashGrid(
                        (pivot + left + right) * (1f / 3f)
                    );
                    hasTower = hash.e < HexMetrics.wallTowerThreshold;
                }
                AddWallSegment(pivot, left, pivot, right, hasTower);
            }
            else if (leftCell.Elevation < rightCell.Elevation)
            {
                AddWallWedge(pivot, left, right);
            }
            else
            {
                AddWallCap(pivot, left);
            }
        }
        else if (hasRighWall)
        {
            if (rightCell.Elevation < leftCell.Elevation)
            {
                AddWallWedge(right, pivot, left);
            }
            else
            {
                AddWallCap(right, pivot);
            }
        }
    }
    /// <summary>
    /// Для создания основных участков стены
    /// </summary>
    /// <param name="nearLeft"></param>
    /// <param name="farLeft"></param>
    /// <param name="nearRight"></param>
    /// <param name="farRight"></param>
    private void AddWallSegment(
        Vector3 nearLeft, Vector3 farLeft, Vector3 nearRight, Vector3 farRight, bool addTower = false
    )
    {
        //заранее искажаем вершины
        nearLeft = HexMetrics.Perturb(nearLeft);
        farLeft = HexMetrics.Perturb(farLeft);
        nearRight = HexMetrics.Perturb(nearRight);
        farRight = HexMetrics.Perturb(farRight);

        //края середины соединения
        Vector3 left = HexMetrics.WallLerp(nearLeft, farLeft);
        Vector3 right = HexMetrics.WallLerp(nearRight, farRight);

        float leftTop = left.y + HexMetrics.wallHeight;
        float rightTop = right.y + HexMetrics.wallHeight;
        //смещение краев(толщина стены)
        Vector3 leftThicknessOffset =
            HexMetrics.WallThicknessOffset(nearLeft, farLeft);
        Vector3 rightThicknessOffset =
            HexMetrics.WallThicknessOffset(nearRight, farRight);

        Vector3 v1, v2, v3, v4;
        v1 = v3 = left - leftThicknessOffset;
        v2 = v4 = right - rightThicknessOffset;
        v3.y = leftTop;
        v4.y = rightTop;
        walls.AddQuadUnperturbed(v1, v2, v3, v4);

        Vector3 t1 = v3, t2 = v4;//верхняя часть стен

        v1 = v3 = left + leftThicknessOffset;
        v2 = v4 = right + rightThicknessOffset;
        v3.y = leftTop;
        v4.y = rightTop;
        walls.AddQuadUnperturbed(v2, v1, v4, v3);

        walls.AddQuadUnperturbed(t1, t2, v3, v4);//верхняя часть стен

        //спавн башни
        if (addTower)
        {
            Transform towerInstance = Instantiate(wallTower);
            towerInstance.transform.localPosition = (left + right) * 0.5f;
            Vector3 rightDirection = right - left;
            rightDirection.y = 0f;
            towerInstance.transform.right = rightDirection;
            towerInstance.SetParent(container, false);
        }
    }

    /// <summary>
    /// Боковая часть стены
    /// </summary>
    /// <param name="near"></param>
    /// <param name="far"></param>
    void AddWallCap(Vector3 near, Vector3 far)
    {
        near = HexMetrics.Perturb(near);
        far = HexMetrics.Perturb(far);

        Vector3 center = HexMetrics.WallLerp(near, far);
        Vector3 thickness = HexMetrics.WallThicknessOffset(near, far);

        Vector3 v1, v2, v3, v4;

        v1 = v3 = center - thickness;
        v2 = v4 = center + thickness;
        v3.y = v4.y = center.y + HexMetrics.wallHeight;
        walls.AddQuadUnperturbed(v1, v2, v3, v4);
    }

    /// <summary>
    /// Соединение со скалами
    /// </summary>
    /// <param name="near"></param>
    /// <param name="far"></param>
    /// <param name="point"></param>
    void AddWallWedge(Vector3 near, Vector3 far, Vector3 point)
    {
        near = HexMetrics.Perturb(near);
        far = HexMetrics.Perturb(far);
        point = HexMetrics.Perturb(point);

        Vector3 center = HexMetrics.WallLerp(near, far);
        Vector3 thickness = HexMetrics.WallThicknessOffset(near, far);

        Vector3 v1, v2, v3, v4;
        Vector3 pointTop = point;
        point.y = center.y;

        v1 = v3 = center - thickness;
        v2 = v4 = center + thickness;
        v3.y = v4.y = pointTop.y = center.y + HexMetrics.wallHeight;

        walls.AddQuadUnperturbed(v1, point, v3, pointTop);
        walls.AddQuadUnperturbed(point, v2, pointTop, v4);
        walls.AddTriangleUnperturbed(pointTop, v3, v4);
    }

    public void AddBridge(Vector3 roadCenter1, Vector3 roadCenter2)
    {
        roadCenter1 = HexMetrics.Perturb(roadCenter1);
        roadCenter2 = HexMetrics.Perturb(roadCenter2);
        Transform instance = Instantiate(bridge);
        instance.localPosition = (roadCenter1 + roadCenter2) * 0.5f;
        instance.forward = roadCenter2 - roadCenter1;
        float length = Vector3.Distance(roadCenter1, roadCenter2);
        Transform parent = instance.parent;
        instance.localScale = new Vector3(
            HexMetrics.bridgeDesignWidth, HexMetrics.bridgeDesignHeight, length * HexMetrics.bridgeDesignLength * (1/HexMetrics.bridgeDesignLength)
        );//вот тут реальные костыли для разной длины мостов
        instance.SetParent(container, false);
    }

    public void AddSpecialFeature(HexCell cell, Vector3 position)
    {
        Transform instance = Instantiate(special[cell.SpecialIndex - 1]);
        instance.localPosition = HexMetrics.Perturb(position);
        HexHash hash = HexMetrics.SampleHashGrid(position);
        instance.localRotation = Quaternion.Euler(0f, 360f * hash.e, 0f);
        instance.SetParent(container, false);
    }

    Transform PickPrefab(HexFeatureCollection[] collection, int level, float hash, float choice)
    {

        if (level > 0)
        {
            float[] thresholds = HexMetrics.GetFeatureThresholds(level - 1);
            for (int i = 0; i < thresholds.Length; i++)
            {
                if (hash < thresholds[i])
                {
                    return collection[i].Pick(choice);
                }
            }
        }
        return null;
    }
}
