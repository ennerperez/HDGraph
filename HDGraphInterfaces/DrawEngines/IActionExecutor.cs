﻿using System;
using System.Collections.Generic;
using System.Text;
using HDGraph.Interfaces.ScanEngines;

namespace HDGraph.Interfaces.DrawEngines
{
    public interface IActionExecutor
    {
        void ExecuteTreeFullRefresh(IDirectoryNode node);

        void ExecuteTreeFillUpToLevel(IDirectoryNode node, int targetLevel);

        void NavigateForward();

        void NavigateBackward();

        /// <summary>
        /// Notify the main windows that the cursor's position has changed and a new new node is hovered.
        /// </summary>
        /// <param name="node"></param>
        void Notify4NewHoveredNode(IDirectoryNode node);

        /// <summary>
        /// Notify the main windows that the root node of the graph has changed.
        /// </summary>
        /// <param name="node"></param>
        void Notify4NewRootNode(IDirectoryNode node);

        /// <summary>
        /// Open a new window showing in a grid the details of the given node.
        /// </summary>
        /// <param name="node"></param>
        void ShowNodeDetails(IDirectoryNode node);

        /// <summary>
        /// Try to define the new root of the current graph : send a request to the current
        /// graph to set the new root.
        /// </summary>
        /// <param name="selectedNode"></param>
        void SetNewRootNode(IDirectoryNode selectedNode);

        void OpenInExplorer(IDirectoryNode iDirectoryNode);

        /// <summary>
        /// Delete the folder.
        /// </summary>
        /// <param name="iDirectoryNode"></param>
        /// <returns>true if action has been started (even if it fails).</returns>
        bool DeleteNode(IDirectoryNode iDirectoryNode);

        bool FolderDeletionIsAllowed { get; }
    }
}
