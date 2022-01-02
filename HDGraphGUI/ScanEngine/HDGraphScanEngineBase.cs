using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Diagnostics;
using HDGraph.Resources;
using HDGraph.ScanEngine;
using HDGraph.Interfaces.ScanEngines;

namespace HDGraph
{
    public abstract class HDGraphScanEngineBase : IXmlSerializable
    {
        public bool IgnoreSymLinks { get; set; }

        #region Vatiables et propri�t�s

        #region Variables avec role de cache
        /// <summary>
        /// Cache messages localis�s
        /// </summary>
        protected static string scanningMessage = null;

        #endregion

        private DirectoryNode root = null;
        /// <summary>
        /// R�pertoire racine du moteur.
        /// </summary>
        public DirectoryNode Root
        {
            get { return root; }
        }

        private DateTime analyzeDate;
        /// <summary>
        /// Date de la derni�re analyse
        /// </summary>
        public DateTime AnalyzeDate
        {
            get { return analyzeDate; }
            set { analyzeDate = value; }
        }

        /// <summary>
        /// Start analyze date.
        /// </summary>
        public DateTime AnalyseStartDate { get; set; }

        public delegate void PrintInfoDelegate(string message);

        private PrintInfoDelegate printInfoDeleg = null;

        /// <summary>
        /// Delegate appel� par le moteur lorsqu'une analyze est en cours.
        /// </summary>
        [XmlIgnore()]
        public PrintInfoDelegate NotifyForNewInfo
        {
            get { return printInfoDeleg; }
            set { printInfoDeleg = value; }
        }

        protected bool pleaseCancelCurrentWork = false;
        /// <summary>
        /// Bool�en indiquant s'il faut stopper l'analyse en cours.
        /// </summary>
        public bool PleaseCancelCurrentWork
        {
            get { return pleaseCancelCurrentWork; }
            set { pleaseCancelCurrentWork = value; }
        }

        protected bool workCanceled = false;
        /// <summary>
        /// Indique si la pr�c�dente analyse a �t� stopp�e en 
        /// raison d'une demande de l'utilisateur.
        /// </summary>
        public bool WorkCanceled
        {
            get { return workCanceled; }
            set { workCanceled = value; }
        }

        private bool autoRefreshAllowed;
        /// <summary>
        /// Bool�en indiquant si le moteur a l'autorisation de compl�ter l'analyse
        /// lorsque le niveau de profondeur est insuffisant pour afficher la totalit� du 
        /// graphique.
        /// </summary>
        public bool AutoRefreshAllowed
        {
            get { return autoRefreshAllowed; }
            set { autoRefreshAllowed = value; }
        }

        private bool showDiskFreeSpace;

        public bool ShowDiskFreeSpace
        {
            get { return showDiskFreeSpace; }
            set
            {
                if (showDiskFreeSpace != value)
                {
                    showDiskFreeSpace = value;
                    ApplyFreeSpaceOption(this.root);
                }

            }
        }

        public List<ScanError> ErrorList { get; set; }

        public List<string> IgnoredSymFoldersList { get; set; }

        #endregion

        #region Contructeur(s)

        public HDGraphScanEngineBase()
        {
            if (scanningMessage == null)
                scanningMessage = ApplicationMessages.Scanning;
            ErrorList = new List<ScanError>();
            IgnoredSymFoldersList = new List<string>();
        }

        #endregion

        #region M�thodes

        /// <summary>
        /// M�thode � appeler pour lancer le scan.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="maxLevel"></param>
        public void BuildTree(string path, int maxLevel)
        {
            if (!Directory.Exists(path))
                throw new ArgumentException("Invalid path.", "path");
            if (path.EndsWith(":"))
                path += @"\";
            pleaseCancelCurrentWork = false;
            workCanceled = false;
            if (maxLevel < 1)
                throw new ArgumentOutOfRangeException("maxLevel", "Il faut afficher au moins 1 niveau !");
            root = new DirectoryNode(path);
            ErrorList = new List<ScanError>();
            IgnoredSymFoldersList = new List<string>();
            AnalyseStartDate = DateTime.Now;
            BuildTreeInternal(root, maxLevel - 1);
            ApplySpecialRootOptions(root);

            analyzeDate = DateTime.Now;
            if (workCanceled)
                root = null;
        }

        /// <summary>
        /// Add to the given node a "free space node" and an "unknown part node", if the given node is a drive root.
        /// </summary>
        /// <param name="root"></param>
        private void ApplySpecialRootOptions(IDirectoryNode root)
        {
            string path = root.Path;
            bool rootNodeIsDrive = PathIsDrive(path);
            if (rootNodeIsDrive)
            {
                // full drive scan, show disk free space if asked
                DriveInfo info = new DriveInfo(path);

                #region Unknown files

                IDirectoryNode dirNode = new DirectoryNode("");
                // Unknown files = taille du disque - espace libre - fichiers trouv�s
                dirNode.TotalSize = info.TotalSize - info.TotalFreeSpace - root.TotalSize;
                dirNode.FilesSize = dirNode.TotalSize;
                dirNode.DirectoryType = SpecialDirTypes.UnknownPart;
                dirNode.ExistsUncalcSubDir = false;
                dirNode.Name = ApplicationMessages.UnknownFiles;
                dirNode.Parent = root;
                if (dirNode.TotalSize < 0)
                {
                    dirNode.Name = String.Format(ApplicationMessages.ErrorNegativeSizeOfUnknownParts, dirNode.Name);
                    Trace.TraceError("Error during folder analysis (" + root.Path +
                                     "): negative size of unkown parts (" +
                                     dirNode.TotalSize + "bytes).");
                    dirNode.TotalSize = 0;
                }
                root.Children.Add(dirNode);
                root.TotalSize += dirNode.TotalSize;

                #endregion

                #region Free disk space

                DirectoryNode dirNodeFS = new DirectoryNode("");
                dirNodeFS.TotalSize = info.TotalFreeSpace;
                if (showDiskFreeSpace)
                {
                    dirNodeFS.DirectoryType = SpecialDirTypes.FreeSpaceAndShow;
                    root.TotalSize += dirNodeFS.TotalSize;
                }
                else
                {
                    dirNodeFS.DirectoryType = SpecialDirTypes.FreeSpaceAndHide;
                }
                dirNodeFS.ExistsUncalcSubDir = false;
                dirNodeFS.Name = ApplicationMessages.FreeSpace;
                dirNodeFS.Parent = root;
                root.Children.Add(dirNodeFS);

                #endregion
            }
        }

        /// <summary>
        /// Indique si le chemin pass� en param�tre repr�sente un lecteur ou non.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static bool PathIsDrive(string path)
        {
            bool rootNodeIsDrive = path.IndexOf('\\') == path.LastIndexOf('\\') // 1 seul '\' dans le chemin
                // le seul '\' du chemin est le dernier car du chemin ( en effet, c:\test n'est pas la racine)                   
                && path.IndexOf('\\') == path.Length - 1;
            return rootNodeIsDrive;
        }

        protected abstract void BuildTreeInternal(IDirectoryNode dir, int maxLevel);

        protected void HandleAnalysisException(IDirectoryNode dir, Exception ex)
        {
            Trace.TraceError("Error during folder analysis (" + dir.Path + "). Folder skiped. Details: " + HDGTools.PrintError(ex));
            dir.Name = String.Format(ApplicationMessages.ErrorLoading, dir.Name, ex.Message);
            dir.DirectoryType = SpecialDirTypes.ScanError;
            ErrorList.Add(new ScanError()
            {
                FileOrDirPath = dir.Path,
                Exception = ex
            });
        }

        /// <summary>
        /// Compl�te une arborescence existante pour lui donner une profondeur max de maxLevel.
        /// I.e. si l'arborescence n'a �t� calcul�e que sur n niveaux et que maxLevel vaut n+4,
        /// la m�thode va calculer les 4 niveaux manquant.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="maxLevel"></param>
        /// <returns></returns>
        public bool FillUpTreeToLevel(IDirectoryNode node, int maxLevel)
        {
            if (!this.autoRefreshAllowed)
                return false;
            if (node == null
                || node.DepthMaxLevel >= maxLevel)
                return true; // rien � faire !

            if (node.DepthMaxLevel == 1 && node.ExistsUncalcSubDir)
            {
                if (node.DirectoryType == SpecialDirTypes.ScanError)
                {
                    // before the new scan, reset the node state
                    node.Name = new DirectoryInfo(node.Path).Name;
                    node.DirectoryType = SpecialDirTypes.NotSpecial;
                }
                long dirPreviousTotalSize = node.TotalSize;
                node.TotalSize = 0;
                node.FilesSize = 0;
                BuildTreeInternal(node, maxLevel - 1);
                if (dirPreviousTotalSize != node.TotalSize)
                    AdjustParentSize(node, node.TotalSize - dirPreviousTotalSize);
            }
            else
            {
                foreach (IDirectoryNode fils in node.Children)
                {
                    FillUpTreeToLevel(fils, maxLevel - 1);
                }
            }
            RefreshFreeSpace(node);
            return true;
        }

        /// <summary>
        /// Incr�mente la taille totale de tous les parents d'un noeud donn�.
        /// </summary>
        /// <param name="node">Noeud dont les parents sont � mettre � jour.</param>
        /// <param name="tailleAjoutee">Montant � ajouter.</param>
        private void AdjustParentSize(IDirectoryNode node, long addedSize)
        {
            if (node.Parent == null)
                return;
            node.Parent.TotalSize += addedSize;
            AdjustParentSize(node.Parent, addedSize);
        }


        public bool RefreshTreeFromNode(IDirectoryNode node)
        {
            if (!this.autoRefreshAllowed)
                return false;

            long dirPreviousTotalSize = node.TotalSize;
            node.TotalSize = 0;
            node.FilesSize = 0;
            node.Children = new List<IDirectoryNode>();
            if (!Directory.Exists(node.Path))
            {
                AdjustParentSize(node, -dirPreviousTotalSize);
                RefreshFreeSpace(node);
                return true;
            }
            BuildTreeInternal(node, node.DepthMaxLevel - 1);
            if (dirPreviousTotalSize != node.TotalSize)
                AdjustParentSize(node, node.TotalSize - dirPreviousTotalSize);
            RefreshFreeSpace(node);
            return true;
        }

        /// <summary>
        /// Refresh the free space size on the root folder of the given node.
        /// </summary>
        /// <param name="theNode"></param>
        private void RefreshFreeSpace(IDirectoryNode theNode)
        {
            IDirectoryNode root = theNode.Root;
            if (PathIsDrive(root.Path))
            {
                // Note: refresh is done as a remove/add options.

                // 1. First of all: identify the special directories
                IDirectoryNode unkownPartNode = null;
                IDirectoryNode freeSpaceNode = null;
                foreach (IDirectoryNode node in root.Children)
                {
                    if (node.DirectoryType == SpecialDirTypes.UnknownPart)
                    {
                        unkownPartNode = node;
                    }
                    if (node.DirectoryType == SpecialDirTypes.FreeSpaceAndHide ||
                        node.DirectoryType == SpecialDirTypes.FreeSpaceAndShow)
                    {
                        freeSpaceNode = node;
                    }
                }
                // 2. Second step : remove the special nodes from the root node
                //   2.1 Remove unknown part node
                if (unkownPartNode != null)
                {
                    root.Children.Remove(unkownPartNode);
                    root.TotalSize -= unkownPartNode.TotalSize;
                }
                //   2.2 Remove free space node
                if (freeSpaceNode != null)
                {
                    root.Children.Remove(freeSpaceNode);
                    if (showDiskFreeSpace)
                    {
                        root.TotalSize -= freeSpaceNode.TotalSize;
                    }
                }

                // 3. Third step : add the special options
                ApplySpecialRootOptions(root);
            }
        }

        #endregion

        #region IXmlSerializable Membres

        public System.Xml.Schema.XmlSchema GetSchema()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        private string[] compatibleVersionsList = new string[] {
                        "1.2.0.0"
                    };


        public void ReadXml(System.Xml.XmlReader reader)
        {
            // D�but �l�ment MoteurGraphiqueur
            reader.ReadStartElement();

            string version = reader.ReadElementContentAsString();

            if (version != AboutBox.AssemblyVersion
                && Array.IndexOf<string>(compatibleVersionsList, version) == -1)
                throw new IncompatibleVersionException();

            analyzeDate = reader.ReadElementContentAsDateTime();

            XmlSerializer serializer = new XmlSerializer(typeof(DirectoryNode));
            root = (DirectoryNode)serializer.Deserialize(reader);

            // Fin �l�ment MoteurGraphiqueur
            reader.ReadEndElement();
        }

        public void WriteXml(System.Xml.XmlWriter writer)
        {
            //string ns = "http://HDGraph.tools.laugel.fr/MoteurGraphiqueur.xsd";

            writer.WriteElementString("HdgVersion", AboutBox.AssemblyVersion);
            writer.WriteElementString("AnalyzeDate", analyzeDate.ToString("s"));

            XmlSerializer serializer = new XmlSerializer(typeof(DirectoryNode));//, ns);
            serializer.Serialize(writer, root);
        }

        #endregion

        /// <summary>
        /// Show or hide the free space on the root folder of the given node.
        /// </summary>
        /// <param name="directoryNode"></param>
        private void ApplyFreeSpaceOption(IDirectoryNode directoryNode)
        {
            if (directoryNode == null)
                return;
            // Rechercher le r�pertoire racine
            IDirectoryNode root = directoryNode;
            while (root.Parent != null)
                root = root.Parent;
            if (PathIsDrive(root.Path))
            {
                DirectoryNode freeSpaceNode = null;
                foreach (DirectoryNode node in root.Children)
                {
                    if (node.DirectoryType == SpecialDirTypes.FreeSpaceAndHide
                        || node.DirectoryType == SpecialDirTypes.FreeSpaceAndShow)
                    {
                        freeSpaceNode = node;
                        break; // sortir du foreach, car le node de l'espace libre est trouv�.
                    }
                }
                if (freeSpaceNode != null)
                {
                    if (this.showDiskFreeSpace
                        && freeSpaceNode.DirectoryType == SpecialDirTypes.FreeSpaceAndHide)
                    {
                        // afficher l'espace libre
                        freeSpaceNode.DirectoryType = SpecialDirTypes.FreeSpaceAndShow;
                        root.TotalSize += freeSpaceNode.TotalSize;
                    }
                    else if (!this.showDiskFreeSpace
                        && freeSpaceNode.DirectoryType == SpecialDirTypes.FreeSpaceAndShow)
                    {
                        // masquer l'espace libre
                        freeSpaceNode.DirectoryType = SpecialDirTypes.FreeSpaceAndHide;
                        root.TotalSize -= freeSpaceNode.TotalSize;
                    }
                }
            }
        }
    }
}
