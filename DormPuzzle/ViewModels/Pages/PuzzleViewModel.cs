﻿using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DormPuzzle.Contracts.ViewModels;
using DormPuzzle.Controls.Blocks;
using DormPuzzle.Game.Tetris;
using DormPuzzle.Helpers;
using DormPuzzle.Models;
using DormPuzzle.Views.Pages;

namespace DormPuzzle.ViewModels.Pages;

public partial class PuzzleViewModel(PuzzlePage puzzlePage) : UViewModel<PuzzlePage>(puzzlePage)
{
    [ObservableProperty]
    private FuckedObservableCollection<Block> blocks = new(typeof(Block).Assembly.GetTypes()
                                                                           .Where(type => type.IsSubclassOf(typeof(Block)))
                                                                           .Select(Activator.CreateInstance)
                                                                           .OrderBy(block => ((Block)block!).Order)
                                                                           .Cast<Block>().ToList());

    [ObservableProperty]
    private int rows = 5;

    [ObservableProperty]
    private int columns = 6;

    [ObservableProperty]
    private FuckedObservableCollection<SolutionBind> solutions = [];

    [ObservableProperty]
    private SolutionBind? selectedSolution;

    [ObservableProperty]
    private bool isLoading;

    public BlockContainer BlockContainer => View.BlockContainer;

    [RelayCommand]
    private void DragCompleted(DragCompletedEventArgs e)
    {
        if (((BlockThumb)e.Source).Block is Block block)
        {
            block = (Block)block.Clone();

            Location location = BlockContainer.PointToLocation(Mouse.GetPosition(BlockContainer));
            location -= block.StartLocation;

            BlockContainer.TryAddBlock(location, block);
        }
    }

    [RelayCommand]
    private void Clear()
    {
        BlockContainer.Clear();
        Solutions = [];
        SelectedSolution = null;
    }

    [RelayCommand]
    private void AddOrRemoveDisabledLocation()
    {
        Location location = BlockContainer.PointToLocation(Mouse.GetPosition(BlockContainer));

        if (BlockContainer.IsEffectiveLocation(location))
        {
            BlockContainer.DisabledLocations.Add(location);
        }
        else
        {
            BlockContainer.DisabledLocations.Remove(location);
        }
    }

    private static ConcurrentDictionary<int, string> SolutionNameCache = new();

    private static string GetSolutionName(int index) =>
        SolutionNameCache.GetOrAdd(index, static index => $"方案 {index}");

    [RelayCommand]
    private async Task Run()
    {
        SolveOptions solveOptions = new(BlockContainer.Columns, BlockContainer.Rows, Blocks.OrderBy(block => block.Order).Select(block => block.Count).ToList())
        {
            Walls = [.. BlockContainer.DisabledLocations],
            KeepTopOnly = true,
        };

        IsLoading = true;

        await Task.Run(() =>
        {
            Solutions = new(
                SolveOptions.Solve(solveOptions)
                .Take(50)
                .Select(static (item, index) => new SolutionBind(GetSolutionName(index), item))
                .ToList()
            );
        });

        if (Solutions.Count > 0)
        {
            SelectedSolution = Solutions.First();
        }

        IsLoading = false;
    }

    partial void OnRowsChanged(int value) => Clear();

    partial void OnColumnsChanged(int value) => Clear();

    partial void OnSelectedSolutionChanged(SolutionBind? value)
    {
        if (value is SolutionBind solutionBind)
        {
            BlockContainer.ClearBlocks();

            foreach (Placement placement in solutionBind.Solution.Placements)
            {
                if (CreateBlock(placement.Block) is Block block)
                {
                    BlockContainer.TryAddBlock(new Location(placement.Y, placement.X), block);
                }
            }
        }
    }

    private Block? CreateBlock(BlockCells blockCells)
    {
        if (Blocks.FirstOrDefault(item => item.Order == blockCells.Type) is Block block)
        {
            block = (Block)block.Clone();
            block.RotateAt(blockCells.Rot * 90);

            return block;
        }

        return null;
    }
}
