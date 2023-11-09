﻿using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using static ToolCore.Utils.Draw;

namespace ToolCore
{
    internal class GridUtils
    {

        private readonly List<MyPhysicalInventoryItem> _tmpItemList = new List<MyPhysicalInventoryItem>();
        private readonly HashSet<IMySlimBlock> _hitBlocksHash = new HashSet<IMySlimBlock>();
        private readonly List<Vector3I> _hitPositions = new List<Vector3I>();

        internal void EmptyBlockInventories(MyCubeBlock block, MyInventory toolInventory)
        {
            for (int i = 0; i < block.InventoryCount; i++)
            {
                MyInventory inventory = block.GetInventory(i);
                if (!inventory.Empty())
                {
                    _tmpItemList.Clear();
                    _tmpItemList.AddRange(inventory.GetItems());
                    foreach (var item in _tmpItemList)
                    {
                        MyInventory.Transfer(inventory, toolInventory, item.ItemId, -1, null, false);
                    }
                }
            }
        }

        public bool IsWithinWorldLimits(out string failedBlockType, long ownerID, string blockName, int pcuToBuild, int blocksToBuild = 0, int blocksCount = 0, Dictionary<string, int> blocksPerType = null)
        {
            var sessionSettings = MyAPIGateway.Session.SessionSettings;
            var blockLimitsEnabled = sessionSettings.BlockLimitsEnabled;
            failedBlockType = null;
            if (blockLimitsEnabled == MyBlockLimitsEnabledEnum.NONE)
            {
                return true;
            }

            ulong steamId = MyAPIGateway.Players.TryGetSteamId(ownerID);
            if (steamId != 0UL && MyAPIGateway.Session.IsUserAdmin(steamId))
            {
                return MyAPIGateway.Session.IsUserIgnorePCULimit(steamId);
            }

            if (sessionSettings.MaxGridSize != 0 && blocksCount + blocksToBuild > sessionSettings.MaxGridSize)
            {
                return false;
            }

            return true;
        }

        internal void GetBlocksInSphere(MyCubeGrid grid, Vector3I min, Vector3I max, Vector3D centre, double radius, ConcurrentCachingList<IMySlimBlock> hitBlocks)
        {
            int i, j, k;
            for (i = min.X; i <= max.X; i++)
            {
                for (j = min.Y; j <= max.Y; j++)
                {
                    for (k = min.Z; k <= max.Z; k++)
                    {
                        var pos = new Vector3I(i, j, k);

                        var posD = (Vector3D)pos;
                        Vector3D corner = Vector3D.Clamp(centre, posD - 0.5, posD + 0.5);

                        var distSqr = Vector3D.DistanceSquared(corner, centre);
                        if (distSqr > radius * radius)
                            continue;

                        MyCube cube;
                        if (!grid.TryGetCube(pos, out cube))
                            continue;

                        var slim = (IMySlimBlock)cube.CubeBlock;
                        if (_hitBlocksHash.Contains(slim))
                            continue;

                        var projector = ((MyCubeGrid)slim.CubeGrid).Projector as IMyProjector;
                        if (projector != null && projector.CanBuild(slim, true) != BuildCheckResult.OK)
                            continue;

                        hitBlocks.Add(slim);
                        _hitBlocksHash.Add(slim);

                    }
                }
            }

            _hitBlocksHash.Clear();
        }

        internal void GetBlocksInCylinder(MyCubeGrid grid, Vector3I min, Vector3I max, Vector3D centre, Vector3D forward, double radius, double length, ConcurrentCachingList<IMySlimBlock> hitBlocks, bool debug = false)
        {
            var endOffset = forward * (length / 2);
            var endOffsetAbs = Vector3D.Abs(endOffset);

            if (debug)
            {
                var centrepoint = grid.GridIntegerToWorld(centre);
                DrawScaledPoint(centrepoint, 0.3f, Color.Green);
                var end1 = grid.GridIntegerToWorld(centre + endOffset);
                var end2 = grid.GridIntegerToWorld(centre - endOffset);
                DrawScaledPoint(end1, 0.3f, Color.Green);
                DrawScaledPoint(end2, 0.3f, Color.Green);
                DrawLine(end1, end2, Color.Green, 0.05f);
            }

            int i, j, k;
            for (i = min.X; i <= max.X; i++)
            {
                for (j = min.Y; j <= max.Y; j++)
                {
                    for (k = min.Z; k <= max.Z; k++)
                    {
                        var pos = new Vector3I(i, j, k);

                        var posD = (Vector3D)pos;
                        var blockOffset = posD - centre;
                        var blockDir = Vector3D.ProjectOnPlane(ref blockOffset, ref forward);

                        var clampedIntersect = Vector3D.Clamp(posD - blockDir, centre - endOffsetAbs, centre + endOffsetAbs);
                        var corner = Vector3D.Clamp(clampedIntersect, posD - 0.5, posD + 0.5);

                        if (debug)
                        {
                            var a = grid.GridIntegerToWorld(clampedIntersect);
                            DrawScaledPoint(a, 0.1f, Color.Blue);

                            var b = grid.GridIntegerToWorld(corner);
                            DrawScaledPoint(b, 0.2f, Color.Red);

                            DrawLine(a, b, Color.Yellow, 0.02f);
                        }

                        var distSqr = Vector3D.DistanceSquared(corner, clampedIntersect);
                        if (distSqr > radius * radius)
                            continue;

                        var cornerOffset = corner - centre;
                        var lengthVector = Vector3D.ProjectOnVector(ref cornerOffset, ref forward);
                        var lengthSqr = lengthVector.LengthSquared();
                        if (lengthSqr > length * length)
                            continue;

                        MyCube cube;
                        if (!grid.TryGetCube(pos, out cube))
                            continue;

                        var slim = (IMySlimBlock)cube.CubeBlock;
                        if (_hitBlocksHash.Contains(slim))
                            continue;

                        var projector = ((MyCubeGrid)slim.CubeGrid).Projector as IMyProjector;
                        if (projector != null && projector.CanBuild(slim, true) != BuildCheckResult.OK)
                            continue;

                        hitBlocks.Add(slim);
                        _hitBlocksHash.Add(slim);

                    }
                }
            }

            _hitBlocksHash.Clear();
        }

        internal void GetBlocksInCuboid(MyCubeGrid grid, Vector3I min, Vector3I max, MyOrientedBoundingBoxD obb, ConcurrentCachingList<IMySlimBlock> hitBlocks)
        {
            int i, j, k;
            for (i = min.X; i <= max.X; i++)
            {
                for (j = min.Y; j <= max.Y; j++)
                {
                    for (k = min.Z; k <= max.Z; k++)
                    {
                        var pos = new Vector3I(i, j, k);

                        var posD = (Vector3D)pos;

                        var box = new BoundingBoxD(posD - 0.5, posD + 0.5);
                        if (obb.Contains(ref box) == ContainmentType.Disjoint)
                            continue;

                        MyCube cube;
                        if (!grid.TryGetCube(pos, out cube))
                            continue;

                        var slim = (IMySlimBlock)cube.CubeBlock;
                        if (_hitBlocksHash.Contains(slim))
                            continue;

                        var projector = ((MyCubeGrid)slim.CubeGrid).Projector as IMyProjector;
                        if (projector != null && projector.CanBuild(slim, true) != BuildCheckResult.OK)
                            continue;

                        hitBlocks.Add(slim);
                        _hitBlocksHash.Add(slim);

                    }
                }
            }

            _hitBlocksHash.Clear();
        }

        internal void GetBlocksOverlappingLine(MyCubeGrid grid, Vector3D start, Vector3D end, ConcurrentCachingList<IMySlimBlock> hitBlocks)
        {
            grid.RayCastCells(start, end, _hitPositions);

            for (int i = 0; i < _hitPositions.Count; i++)
            {
                var pos = _hitPositions[i];

                MyCube cube;
                if (!grid.TryGetCube(pos, out cube))
                    continue;

                var slim = (IMySlimBlock)cube.CubeBlock;
                if (_hitBlocksHash.Contains(slim))
                    continue;

                var projector = ((MyCubeGrid)slim.CubeGrid).Projector as IMyProjector;
                if (projector != null && projector.CanBuild(slim, true) != BuildCheckResult.OK)
                    continue;

                hitBlocks.Add(slim);
                _hitBlocksHash.Add(slim);
            }

            _hitBlocksHash.Clear();
        }

        internal void GetBlockInRayPath(MyCubeGrid grid, Vector3D pos, ConcurrentCachingList<IMySlimBlock> hitBlocks, bool debug = false)
        {
            var localPos = Vector3D.Transform(pos, grid.PositionComp.WorldMatrixNormalizedInv);
            var cellPos = Vector3I.Round(localPos * grid.GridSizeR);

            if (debug)
                DrawScaledPoint(pos, 0.25, Color.Violet);

            MyCube cube;
            if (!grid.TryGetCube(cellPos, out cube))
                return;

            var slim = (IMySlimBlock)cube.CubeBlock;
            //if (_hitBlocksHash.Contains(slim))
            //    return;

            var projector = ((MyCubeGrid)slim.CubeGrid).Projector as IMyProjector;
            if (projector != null && projector.CanBuild(slim, true) != BuildCheckResult.OK)
                return;

            hitBlocks.Add(slim);
            //_hitBlocksHash.Add(slim);
        }
    }
}
