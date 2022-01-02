using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using HDGraph.Resources;
using HDGraph.DrawEngine;
using System.Drawing.Imaging;
using HDGraph.Interfaces.ScanEngines;
using HDGraph.Interfaces.DrawEngines;

namespace HDGraph
{
    public partial class TreeGraph : UserControl, IManualRefreshControl
    {
        #region Variables et propri�t�s

        /// <summary>
        /// Objet DirectoryNode qui doit �tre consid�r� comme la racine du graphe.
        /// </summary>
        private IDirectoryNode root;

        public IDirectoryNode Root
        {
            get { return root; }
            set
            {
                root = value;
                if (actionExecutor != null)
                    actionExecutor.Notify4NewRootNode(value);
            }
        }

        private Pen graphPen = new Pen(Color.Black, 1.0f);

        public Pen GraphPen
        {
            get { return graphPen; }
            set { graphPen = value; }
        }

        [ReadOnly(true)]
        public override Font Font
        {
            get
            {
                return base.Font;
            }
            set
            {
                base.Font = value;
                drawOptions.TextFont = value;
            }
        }

        /// <summary>
        /// Impose au composant de se redessiner, m�me si sa taille n'a pas chang�.
        /// </summary>
        private bool forceRefreshOnNextRepaint = false;

        public bool ForceRefreshOnNextRepaint
        {
            get { return forceRefreshOnNextRepaint; }
            set { forceRefreshOnNextRepaint = value; }
        }


        /// <summary>
        /// Coordonn�es du curseur de la souris � l'int�rieur du contr�le, lors du dernier clic effectu� sur le contr�le.
        /// Utilis� par exemple lors du chargement du menu contextuel, pour savoir sur quel r�pertoire du graph le clic droit a �t� effectu�.
        /// </summary>
        private Point? lastClicPosition = null;
        /// <summary>
        /// Idem que lastClicPosition, mais stocke le directoryNode directement et non les coordonn�es du curseur. 
        /// Est utilis� lorsque lastClicPosition a �t� d�finit.
        /// </summary>
        private IDirectoryNode lastClicNode = null;

        /// <summary>
        /// Bitmap buffer dans lequel le graph est dessin�.
        /// </summary>
        private Bitmap backBuffer;

        /// <summary>
        /// Obtient le gtaph sous forme d'image.
        /// </summary>
        internal Bitmap ImageBuffer
        {
            get { return backBuffer; }
        }

        /// <summary>
        /// User is currently rotating the graph : don't rebuild the full image, only rotate the
        /// image as preview.
        /// When finished, rebuild the full image with the text.
        /// </summary>
        public bool RotationInProgress { get; set; }
        /// <summary>
        /// User is currently changing the text property (text size, text density, etc).
        /// Don't rebuild the full image : just rebuild the text part.
        /// </summary>
        public bool TextChangeInProgress { get; set; }

        private IActionExecutor actionExecutor;

        #endregion

        #region Constructeur

        public TreeGraph(IActionExecutor actionExecutor)
            : this()
        {
            this.actionExecutor = actionExecutor;
        }

        public TreeGraph()
        {
            InitializeComponent();
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer, true);
        }

        void DrawOptions_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ShowTooltip")
            {
                // Nothing to do.
                return;
            }
            if (e.PropertyName == "ShowSize")
            {
                TextChangeInProgress = true;
                ForceRefresh();
                TextChangeInProgress = false;
            }
            else
            {
                this.ForceRefresh();
            }
        }

        #endregion

        #region M�thodes
        private void TreeGraph_Load(object sender, EventArgs e)
        {
            if (drawOptions == null)
                drawOptions = new DrawOptions();
            drawOptions.SuspendPropertyChangedNotifications = true;
            drawOptions.TextFont = this.Font;
            drawOptions.SuspendPropertyChangedNotifications = false;
        }


        public enum CalculationState
        {
            None,
            InProgress,
            Finished
        }

        private CalculationState calculationState;

        private bool resizing;

        public bool Resizing
        {
            get { return resizing; }
            set { resizing = value; }
        }

        private DrawOptions drawOptions;

        public DrawOptions DrawOptions
        {
            get { return drawOptions; }
            set
            {
                if (drawOptions != null)
                    drawOptions.PropertyChanged -= new PropertyChangedEventHandler(DrawOptions_PropertyChanged);
                drawOptions = value;
                if (drawOptions != null)
                    drawOptions.PropertyChanged += new PropertyChangedEventHandler(DrawOptions_PropertyChanged);
            }
        }

        public DrawType DrawType { get; set; }

        /// <summary>
        /// M�thode classique OnPaint surcharg�e pour afficher le graph, et le calculer si n�cessaire.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnPaint(PaintEventArgs e)
        {
            bool sizeChanged = (backBuffer == null
                                || backBuffer.Width != this.ClientSize.Width
                                || backBuffer.Height != this.ClientSize.Height);
            Bitmap backBufferTmp = backBuffer;
            try
            {
                if (backBuffer == null)
                {
                    // tout premier init.
                    drawOptions.TargetSize = this.ClientSize;
                    ImageGraphGeneratorBase generator = ImageGraphGeneratorFactory.CreateGenerator(this.DrawType, this.Root);
                    forceRefreshOnNextRepaint = true;
                    this.backgroundWorker1_DoWork(this, new DoWorkEventArgs(generator));
                }
                if (sizeChanged || forceRefreshOnNextRepaint)
                {
                    ImageGraphGeneratorBase generator;
                    if (resizing)
                        backBufferTmp = TransformToWaitImage(this.backBuffer, this.ClientSize, ApplicationMessages.ResizeInProgressByUser, true);
                    // Abandonn�.
                    //else if (RotationInProgress)
                    //{
                    //    Bitmap image = RotateImage(imageOnlyBackBuffer, drawOptions.ImageRotation);
                    //    backBufferTmp = TransformToWaitImage(image, this.ClientSize, ApplicationMessages.RotateInProgressByUser);
                    //}
                    else
                        switch (calculationState)
                        {
                            case CalculationState.None:
                                calculationState = CalculationState.InProgress;
                                backBufferTmp = TransformToWaitImage(this.backBuffer, this.ClientSize, ApplicationMessages.PleaseWaitWhileDrawing, false);

                                // lancement du calcul
                                // Calcul
                                drawOptions.TargetSize = this.ClientSize;
                                generator = ImageGraphGeneratorFactory.CreateGenerator(this.DrawType, this.Root);
                                backgroundWorker1.RunWorkerAsync(generator);
                                break;
                            case CalculationState.InProgress:
                                backBufferTmp = TransformToWaitImage(this.backBuffer, this.ClientSize, ApplicationMessages.PleaseWaitWhileDrawing, false);
                                break;
                            case CalculationState.Finished:
                                if (backBuffer.Size != this.ClientSize
                                    || !drawOptions.Equals(lastCompletedGraphOption))
                                {
                                    calculationState = CalculationState.InProgress;
                                    backBufferTmp = TransformToWaitImage(this.backBuffer, this.ClientSize, ApplicationMessages.PleaseWaitWhileDrawing, false);

                                    // lancement du calcul
                                    // Calcul
                                    drawOptions.TargetSize = this.ClientSize;
                                    generator = ImageGraphGeneratorFactory.CreateGenerator(this.DrawType, this.Root);
                                    backgroundWorker1.RunWorkerAsync(generator);
                                }
                                else
                                {
                                    backBufferTmp = backBuffer;
                                    forceRefreshOnNextRepaint = false;
                                    calculationState = CalculationState.None;
                                }
                                break;
                            default:
                                throw new NotSupportedException("Value of calculationState (" + calculationState + ") is not supported.");
                        }
                }


            }
            catch (Exception ex)
            {
                // TODO : g�rer exception.
                backBufferTmp = new Bitmap(this.ClientSize.Width, this.ClientSize.Height);
                Graphics errorGraph = Graphics.FromImage(backBufferTmp);
                errorGraph.Clear(Color.White);
                errorGraph.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                errorGraph.DrawString(ex.ToString(), this.ParentForm.Font, new SolidBrush(Color.Black), new RectangleF(0, 0, this.ClientSize.Width, this.ClientSize.Height));
            }
            // affichage du buffer
            e.Graphics.DrawImageUnscaled(backBufferTmp, 0, 0);

        }

        private Bitmap RotateImage(Bitmap oldUnchangedImage, float angle)
        {
            //create a new empty bitmap to hold rotated image
            Bitmap returnBitmap = new Bitmap(oldUnchangedImage.Width, oldUnchangedImage.Height);
            //make a graphics object from the empty bitmap
            Graphics g = Graphics.FromImage(returnBitmap);
            g.Clear(Color.White);
            //move rotation point to center of image
            g.TranslateTransform((float)oldUnchangedImage.Width / 2, (float)oldUnchangedImage.Height / 2);
            //rotate
            g.RotateTransform(angle);
            //move image back
            g.TranslateTransform(-(float)oldUnchangedImage.Width / 2, -(float)oldUnchangedImage.Height / 2);
            //draw passed in image onto graphics object
            g.DrawImage(oldUnchangedImage, new Point(0, 0));
            return returnBitmap;
        }

        private Bitmap TransformToWaitImage(Bitmap originalBitmap, Size clientSize, string message, bool printMessageForSmallGraph)
        {
            if (originalBitmap == null)
                return null;
            Bitmap newBitmap = new Bitmap(clientSize.Width, clientSize.Height);
            using (Graphics g = Graphics.FromImage(newBitmap))
            {

                float originalRatio = originalBitmap.Height / (float)originalBitmap.Width;
                float newRatio = clientSize.Height / (float)clientSize.Width;

                float ratio = Math.Min(originalRatio, newRatio);

                float hScale = (float)clientSize.Height / originalBitmap.Height;
                float wScale = (float)clientSize.Width / originalBitmap.Width;
                Rectangle targetRectangle = new Rectangle();


                float newWidth = originalBitmap.Width * clientSize.Height / (float)originalBitmap.Height;
                if (newWidth > clientSize.Width)
                {
                    targetRectangle.Width = clientSize.Width;
                    targetRectangle.Height = Convert.ToInt32(originalBitmap.Height * clientSize.Width / (float)originalBitmap.Width);

                    targetRectangle.X = 0;
                    targetRectangle.Y = Math.Abs(targetRectangle.Height - clientSize.Height) / 2;
                }
                else
                {
                    targetRectangle.Width = Convert.ToInt32(newWidth);
                    targetRectangle.Height = clientSize.Height;

                    targetRectangle.X = targetRectangle.Y = Math.Abs(targetRectangle.Width - clientSize.Width) / 2;
                    targetRectangle.Y = 0;
                }
                g.DrawImage(originalBitmap, targetRectangle);

                bool printMessage = Root != null
                                    && !TextChangeInProgress
                                    && (Root.HasMoreChildrenThan(NB_MAX_OF_SUB_DIR_BEFORE_WAIT_MESSAGE)
                                        || printMessageForSmallGraph);
                if (printMessage)
                {
                    // Print wait message
                    Brush brush = new SolidBrush(Color.FromArgb(100, 255, 255, 255));
                    g.FillRectangle(brush, 0, 0, newBitmap.Width, newBitmap.Height);
                    brush = new SolidBrush(Color.FromArgb(50, 0, 0, 0));
                    g.FillRectangle(brush, 0, 0, newBitmap.Width, newBitmap.Height);

                    Font font = new Font(System.Drawing.FontFamily.GenericSerif, 24, FontStyle.Bold);
                    DrawHelper.PrintTextInTheMiddle(g, clientSize, message, font, new SolidBrush(Color.Black), true);
                }

            }
            return newBitmap;
        }

        /// <summary>
        /// When the total number of directories on the graph is bigger than this 
        /// value, a wait message is shown to the user during the graph draw.
        /// </summary>
        private const int NB_MAX_OF_SUB_DIR_BEFORE_WAIT_MESSAGE = 400;

        private Bitmap ConstruireMulticolorTree(Bitmap newMutlicolorBitmap)
        {
            Rectangle pieRec;
            if (ClientSize.Height > ClientSize.Width)
                pieRec = new Rectangle(-(ClientSize.Height - ClientSize.Width) / 2, 0, ClientSize.Height, ClientSize.Height);
            else
                pieRec = new Rectangle(0, -(ClientSize.Width - ClientSize.Height) / 2, ClientSize.Width, ClientSize.Width);
            Graphics graph = Graphics.FromImage(newMutlicolorBitmap);
            int nbMaxQuartiers = 1000;
            for (int i = 0; i < nbMaxQuartiers; i++)
            {
                float startAngle = (360f / nbMaxQuartiers) * i;
                float nodeAngle = 360f / nbMaxQuartiers;
                PointF p1 = new PointF();
                p1.X = pieRec.Left + pieRec.Width / 2f + Convert.ToSingle(Math.Cos(MathHelper.GetRadianFromDegree(startAngle))) * pieRec.Height / 2f;
                p1.Y = pieRec.Top + pieRec.Height / 2f + Convert.ToSingle(Math.Sin(MathHelper.GetRadianFromDegree(startAngle))) * pieRec.Height / 2f;
                PointF p2 = new PointF();
                p2.X = pieRec.Left + pieRec.Width / 2f + Convert.ToSingle(Math.Cos(MathHelper.GetRadianFromDegree(startAngle + nodeAngle))) * pieRec.Height / 2f;
                p2.Y = pieRec.Top + pieRec.Height / 2f + Convert.ToSingle(Math.Sin(MathHelper.GetRadianFromDegree(startAngle + nodeAngle))) * pieRec.Height / 2f;


                graph.FillPie(
                      new System.Drawing.Drawing2D.LinearGradientBrush(
                            p1, p2,
                            ColorManager.ColorByLeft(Convert.ToInt32(startAngle / 360f * 1000f), 1000),
                            ColorManager.ColorByLeft(Convert.ToInt32((startAngle + nodeAngle) / 360f * 1000f), 1000)
                      ),
                      pieRec, startAngle, nodeAngle + 1);
            }
            return newMutlicolorBitmap;
        }






        private void TreeGraph_Resize(object sender, EventArgs e)
        {
            Refresh();
        }

        /// <summary>
        /// Lance la m�thode point�e par le delegate UpdateHoverNode, 
        /// pour signifier au client qu'un r�pertoire est en ce moment survol�.
        /// Met �galement � jour le curseur courant et l'infoBulle du r�pertoire survol�.
        /// </summary>
        private void SendPointedNode()
        {
            IDirectoryNode foundNode = (lastGeneratorCompleted == null) ? null
                        : lastGeneratorCompleted.FindNodeByCursorPosition(PointToClient(Cursor.Position));
            if (foundNode == null)
            {
                this.Cursor = System.Windows.Forms.Cursors.Default;
                HideToolTip();
            }
            else
            {
                this.Cursor = System.Windows.Forms.Cursors.Hand;
                UpdateOrCreateToolTip(foundNode);
            }

            if (actionExecutor != null)
                actionExecutor.Notify4NewHoveredNode(foundNode);
        }

        private ToolTip toolTip;

        /// <summary>
        /// Ensure the correct tooltip is affected to the current userControl, according to the given node.
        /// </summary>
        /// <param name="foundNode"></param>
        private void UpdateOrCreateToolTip(IDirectoryNode foundNode)
        {
            if (!DrawOptions.ShowTooltip
                || this.calculationState == CalculationState.InProgress)
                return;
            if (toolTip != null
                && (string)toolTip.Tag != foundNode.Path)
                HideToolTip();
            if (toolTip == null)
            {
                toolTip = new ToolTip();
                toolTip.Tag = foundNode.Path;
                toolTip.IsBalloon = true;

                string toolTipText = foundNode.Name + Environment.NewLine + foundNode.HumanReadableTotalSize;
                toolTip.SetToolTip(this, toolTipText);
            }
        }

        /// <summary>
        /// Hide a previous affected tooltip.
        /// </summary>
        private void HideToolTip()
        {
            if (toolTip != null)
            {
                toolTip.RemoveAll();
                //.Hide(this.ParentForm);
                toolTip = null;
            }

        }



        private void TreeGraph_MouseMove(object sender, MouseEventArgs e)
        {
            SendPointedNode();
        }

        private void TreeGraph_MouseDown(object sender, MouseEventArgs e)
        {
            lastClicPosition = PointToClient(Cursor.Position);
        }

        /// <summary>
        /// Chargement du menu contextuel lors du clic droit sur le contr�le.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            if (!lastClicPosition.HasValue)
                return;

            IDirectoryNode node = (lastGeneratorCompleted == null) ? null
                        : lastGeneratorCompleted.FindNodeByCursorPosition(lastClicPosition.Value);
            lastClicNode = node;
            bool nodeIsNotNull = (node != null);
            bool nodeIsRegularNode = nodeIsNotNull
                                    && node.DirectoryType == SpecialDirTypes.NotSpecial;
            if (node == null)
            {
                e.Cancel = true;
                return;
            }
            directoryNameToolStripMenuItem.Enabled = nodeIsNotNull;
            centerGraphOnThisDirectoryToolStripMenuItem.Enabled = (nodeIsRegularNode
                                                                   && node != root);
            centerGraphOnParentDirectoryToolStripMenuItem.Enabled = (nodeIsRegularNode
                                                                && node.Parent != root
                                                                && node.Parent != null);
            openThisDirectoryInWindowsExplorerToolStripMenuItem.Enabled = nodeIsRegularNode;
            refreshThisDirectoryToolStripMenuItem.Enabled = nodeIsRegularNode;

            // Item "Suppression"
            deleteToolStripMenuItem.Enabled = nodeIsRegularNode && Properties.Settings.Default.OptionAllowFolderDeletion;

            // Item "Titre du dossier"
            if (nodeIsNotNull)
                directoryNameToolStripMenuItem.Text = node.Name + " (" + HDGTools.FormatSize(node.TotalSize) + ")";
            else
                directoryNameToolStripMenuItem.Text = "/";
        }

        private void centerGraphOnThisDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CenterGraphOnThisDirectory();
        }

        /// <summary>
        /// Centre le graph sur le r�pertoire design� par lastClicNode.
        /// </summary>
        private void CenterGraphOnThisDirectory()
        {
            if (lastClicNode != null)
            {
                this.root = lastClicNode;
                actionExecutor.ExecuteTreeFillUpToLevel(root, drawOptions.ShownLevelsCount);
                actionExecutor.Notify4NewRootNode(this.root);
            }
            ForceRefresh();
        }

        private void centerGraphOnParentDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CenterGraphOnParentDirectory();
        }

        /// <summary>
        /// Centre le graph sur le r�pertoire parent de lastClicNode.
        /// </summary>
        private void CenterGraphOnParentDirectory()
        {
            if (lastClicNode != null && lastClicNode.Parent != null)
            {
                this.Root = lastClicNode.Parent;
                actionExecutor.Notify4NewRootNode(this.root);
            }
            ForceRefresh();
        }

        /// <summary>
        /// Ouvre le r�pertoire d�sign� par lastClicNode dans l'explorateur.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void openThisDirectoryInWindowsExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenSelectedFolderInExplorer();
        }

        private void OpenSelectedFolderInExplorer()
        {
            if (lastClicNode != null
                && !String.IsNullOrEmpty(lastClicNode.Path))
                actionExecutor.OpenInExplorer(lastClicNode);
        }

        /// <summary>
        /// Force le rafraichissement du contr�le (m�me si le graph n'a pas chang�).
        /// </summary>
        public void ForceRefresh()
        {
            forceRefreshOnNextRepaint = true;
            this.Refresh();
        }

        /// <summary>
        /// G�re le double clic sur le contr�le (recentrage du graph sur le r�pertoire cliqu� ou sur le parent du r�pertoire cliqu�).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeGraph_DoubleClick(object sender, EventArgs e)
        {
            lastClicPosition = PointToClient(Cursor.Position);

            lastClicNode = (lastGeneratorCompleted == null) ? null
                        : lastGeneratorCompleted.FindNodeByCursorPosition(lastClicPosition.Value);
            if (lastClicNode != null)
            {
                if (lastClicNode == root)
                    CenterGraphOnParentDirectory();
                else
                    CenterGraphOnThisDirectory();
            }

        }

        /// <summary>
        /// Demande le rafraichissement du r�pertoire de l'arborescence point� par la souris.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void refreshThisDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RafraichirArboDuDernierClic();
        }

        #endregion

        /// <summary>
        /// Rafraichit l'arborescence vis�e par le dernier clic.
        /// </summary>
        private void RafraichirArboDuDernierClic()
        {
            if (lastClicNode != null)
            {
                actionExecutor.ExecuteTreeFullRefresh(lastClicNode);
            }
            ForceRefresh();
        }

        /// <summary>
        /// Supprime un dossier.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void deletePermanentlyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (actionExecutor.DeleteNode(lastClicNode))
                RafraichirArboDuDernierClic();
        }



        private void directoryNameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ContextMenuStrip.Show();
        }


        private void detailsViewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (actionExecutor != null && lastClicNode != null)
                actionExecutor.ShowNodeDetails(lastClicNode);
        }


        private DrawOptions lastCompletedGraphOption;
        private Bitmap imageOnlyBackBuffer;
        private ImageGraphGeneratorBase lastGeneratorCompleted;

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            ImageGraphGeneratorBase generator = e.Argument as ImageGraphGeneratorBase;
            if (generator == null)
                return;
            DrawOptions currentOptions = drawOptions.Clone();
            lastCompletedGraphOption = null;
            if (!TextChangeInProgress)
            {
                BiResult<Bitmap, DrawOptions> imageResult = generator.Draw(true, false, currentOptions);
                imageOnlyBackBuffer = imageResult.Obj1;
                lastCompletedGraphOption = imageResult.Obj2;
            }
            BiResult<Bitmap, DrawOptions> textResult = generator.Draw(false, true, currentOptions);
            Bitmap textBackBufferTmp = textResult.Obj1;
            if (lastCompletedGraphOption == null)
                lastCompletedGraphOption = textResult.Obj2;
            Bitmap backBufferTmp = (Bitmap)imageOnlyBackBuffer.Clone();
            Graphics.FromImage(backBufferTmp).DrawImage(textBackBufferTmp, new Point(0, 0));
            lastGeneratorCompleted = generator;
            backBuffer = backBufferTmp;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            forceRefreshOnNextRepaint = true;
            calculationState = CalculationState.Finished;

            this.Invoke(new EventHandler(this.InternalRefresh));
            //this.Refresh();
        }

        private void InternalRefresh(Object sender, EventArgs args)
        {
            this.Refresh();
        }
    }
}
