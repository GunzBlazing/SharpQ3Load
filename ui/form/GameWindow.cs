using System;
using System.Drawing;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using System.Diagnostics;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using engine;
using utilities;

#pragma warning disable CS1591

namespace sharpq3load_ui
{
	/// <summary>
	/// Console for driving entire application
	/// </summary>
	public partial class GameWindow : Form, obsvr.IObserverHelper
	{
		private Engine m_Engine = null;

		private bool m_bRunning = false;
		private bool m_bPostOpen = false;
		private bool m_bOpeningMap = false;
		private bool m_bPaused = false;
		private bool m_bClosed = false;

		MoveStates m_lastTickMovestates = new MoveStates();

		private IntPtr m_mainDC = IntPtr.Zero;
		private IntPtr m_mainRC = IntPtr.Zero;
        private Stopwatch m_swFramerate = new Stopwatch();
        private Stopwatch m_swDelayMusicStart = new Stopwatch();
        private SoundManager m_SoundManager = new SoundManager();
        private int m_nFrameCounter = 0;
        private double m_dFPS = 0.0;
		private double m_dVelocity = 0.0;
		Dictionary<Keys, bool> m_dictKeyStates = new Dictionary<Keys, bool>();
		Dictionary<MouseButtons, bool> m_dictMouseButtonStates = new Dictionary<MouseButtons, bool>();
		private bool m_bMiddleMouseUp = false;

		private OpenGLControlModded.simpleOpenGlControlEx m_openGLControl;
		private obsvr.Communicator m_Observer = new obsvr.Communicator();
		private MapChooserForm m_menu;

		Point m_CursorPoint = new Point();
		private Zipper m_zipper = new Zipper();

		Keys m_RecentKey;

		gl_font.BasicFont m_fonter = null;

		/// <summary>
		/// Global engine to be accessed by MapProgress
		/// </summary>
		public static Engine static_theEngine = null;

		/// <summary>
		/// Global map to be accessed by MapProgress
		/// </summary>
		public static MapInfo static_theMap = null;

		/// <summary>
		/// Global semaphore to keep track of cursor visibility
		/// </summary>
		public static bool m_bCursorShown = true;

		/// <summary>
		/// Global static indicator of map load progress
		/// </summary>
		public static bool m_bLoadingMap = false;

		/// <summary>
		/// Initialize a Simulator
		/// </summary>
		public GameWindow()
		{
			InitializeComponent();

			m_Observer.SetListener(this);

			m_menu = new MapChooserForm();

			m_openGLControl.ProcessKey += new KeyEventHandler(m_openGLControl_ProcessKey);

			timerRedrawer.Interval = 15;

			SetViewMode(true);

			GameGlobals.m_InstanceStopWatch.Start(); // start this once and let it go.
			GameGlobals.InitTables(m_zipper);
		}

        private void SetupProjectionMatrix(float fFOV)
        {
            GL.MatrixMode(MatrixMode.Projection);

            GL.DepthFunc(DepthFunction.Lequal);
            GL.Hint(HintTarget.PerspectiveCorrectionHint, HintMode.Nicest);

            GL.LoadIdentity();
            // .07 for near was chosen so the depth test works well on faces close to eachother. Also, it is chosen so it's close to you.
            // changed it to .2 to make walls work in long hallway of dm0
            Matrix4 persMat = Matrix4.CreatePerspectiveFieldOfView(fFOV * (float)GLB.DegToRad, (float)m_openGLControl.Width / (float)m_openGLControl.Height, .2f, 200.0f);
            GL.LoadMatrix(ref persMat);

            GL.MatrixMode(MatrixMode.Modelview);
        }

        private void Simulator_Load(object sender, EventArgs e)
		{
			m_fonter = new gl_font.BasicFont();

			GL.ShadeModel(ShadingModel.Smooth);

			GL.Enable(EnableCap.CullFace);
			GL.CullFace(CullFaceMode.Front);

			GL.ClearColor(System.Drawing.Color.Black);			

			GL.Enable(EnableCap.DepthTest);

            SetupProjectionMatrix(GameGlobals.GetFOVOut());

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

			m_openGLControl.Focus();

			SetCursor(false, false);

			OpenMap();
		}				

		private void SetViewMode(bool bSimming)
		{
			SuspendLayout();
			if (bSimming == true)
			{
				m_openGLControl.Visible = true;
				if (m_tlContainer.Controls.Contains(m_controlMapProgress))
				{
					m_tlContainer.Controls.Remove(m_controlMapProgress);
					Debug.Assert(!m_tlContainer.Controls.Contains(m_controlMapProgress));
				}
				m_controlMapProgress.Visible = false;
				if (!m_tlContainer.Controls.Contains(m_openGLControl))
				{
					m_tlContainer.Controls.Add(m_openGLControl, 0, 0);
					Debug.Assert(m_tlContainer.Controls.Contains(m_openGLControl));
				}
				m_tlContainer.SetRowSpan(m_openGLControl, 3);
				m_tlContainer.SetColumnSpan(m_openGLControl, 3);
				m_openGLControl.Dock = DockStyle.Fill;
				m_tlContainer.Dock = DockStyle.Fill;
			}
			else 
			{
				m_controlMapProgress.Visible = true;
				if(m_tlContainer.Controls.Contains(m_openGLControl))
				{
					m_tlContainer.Controls.Remove(m_openGLControl);
					Debug.Assert(!m_tlContainer.Controls.Contains(m_openGLControl));
				}
				m_openGLControl.Visible = false;
				if(!m_tlContainer.Controls.Contains(m_controlMapProgress))
				{
					m_tlContainer.Controls.Add(m_controlMapProgress, 1, 1);
					Debug.Assert(m_tlContainer.Controls.Contains(m_controlMapProgress));
				}
				m_tlContainer.SetRowSpan(m_controlMapProgress, 1);
				m_tlContainer.SetColumnSpan(m_controlMapProgress, 1);
				m_controlMapProgress.Dock = DockStyle.Fill;
				m_tlContainer.Dock = DockStyle.Fill;
			}
			ResumeLayout();
		}

		/// <summary>
		/// Set state of cursor
		/// </summary>
		/// <param name="bShow">true to show, false to hide</param>
		/// <param name="bHalting">override cursor error checking because program has crashed and we don't care</param>
		public static void SetCursor(bool bShow, bool bHalting)
		{
			if ((m_bCursorShown && bShow && !bHalting) || (!m_bCursorShown && !bShow && !bHalting))
				throw new Exception("Invalid cursor operation");

			if (bShow) Cursor.Show();
			else Cursor.Hide();

			m_bCursorShown = bShow;
		}

		private void workerthread_LoadMap(object sender, DoWorkEventArgs e)
		{
			m_bLoadingMap = true;

			m_controlMapProgress.Details("Loading map");
			static_theEngine.LoadMap(static_theMap);

			m_controlMapProgress.DoneLoadingMap();

			m_controlMapProgress.Details("Initializing engine");
			static_theEngine.Initialize();

			m_controlMapProgress.DoneCreatingBoundingBoxes();

			m_openGLControl.MakeCurrent();

			m_controlMapProgress.Details("Initializing vertex buffers and display lists"); // do open gl stuff
			static_theEngine.InitializeLists();

			m_openGLControl.Context.MakeCurrent(null);

			m_controlMapProgress.Details("Cleaning up map");
			static_theMap.CleanUpMap();

			m_controlMapProgress.Details("Finalizing");
			m_controlMapProgress.DoneInitializingLists();

			LOGGER.Info("Exiting Loadmap thread");
		}

		/// <summary>
		/// Gets run after map load is done. Back in main thread.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void mainthread_LoadMapCompleted(object sender, RunWorkerCompletedEventArgs e)
 		{
			LOGGER.Info("Entered load map completed function");

			SetCursor(false, false);
			m_SoundManager.Clear();

			m_openGLControl.MakeCurrent();

			m_bLoadingMap = false;
			m_bClosed = false;

			SetViewMode(true);

			m_openGLControl.Focus();

			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			GL.Flush();

			SetupProjectionMatrix(GameGlobals.GetFOVOut());

			m_Engine.showScene(GetRecentKey);

			m_bPostOpen = true;

			resetMouseCursor();

			SetRunning(true);

			StartStopRedrawer(true);

            m_SoundManager.PlayEffect(SoundManager.EEffects.SPAWN);
            m_swDelayMusicStart.Reset(); // it could be going if you open a map right after opening a different one
            m_swDelayMusicStart.Start();
		}

		private void SetRunning(bool bOnOff)
		{
			m_bRunning = bOnOff;
		}

		/// <summary>
		/// Halt rendering and show cursor
		/// </summary>
		public void Halt()
		{
			// disable timer to stop opengl refreshing and mouse cursor resetting
			SetRunning(false);
			StopAllTimers();

			SetCursor(true, true);
		}

		/// <summary>
		/// Pop map chooser and open a new map
		/// </summary>
		private void OpenMap()
		{
			m_bOpeningMap = true;

			// disable timer to stop opengl refreshing and mouse cursor resetting
			SetRunning(false);
			StopAllTimers();

			SetCursor(true, false);

            m_SoundManager.Stop();

			m_menu.ClearChosenMap();
			m_menu.ShowDialog();

			MapInfo map = m_menu.GetChosenMap;
			if (map != null && map.ExtractedFromZip)
				map.GetMapPathOnDisk = m_zipper.ExtractMap(map.GetPath);

			ProcessMap(map);

			if (m_menu.GetExittedProgram()) ExitProgram();
		}

        private void ExitProgram()
        {
            static_theEngine = null;
            static_theMap = null;

            if (m_Engine != null) m_Engine.Delete();
            if (m_fonter != null) m_fonter.Delete();

            m_SoundManager.Dispose();

            while (!m_SoundManager.GetPlaybackStopped())
            {
                System.Threading.Thread.Sleep(100);
            }

            Close();
        }

        public void Notify(obsvr.Change pChange) // this is more like an update call. it's a way to get around c# single inheritance
        {
			// handle frame of view change
			if(pChange.GetCode == (int)Engine.ESignals.ZOOMED_IN)
            {
				SetupProjectionMatrix(GameGlobals.GetFOVIn());
			}
			else if(pChange.GetCode == (int)Engine.ESignals.ZOOMED_OUT)
            {
				SetupProjectionMatrix(GameGlobals.GetFOVOut());
			}
        }

		private void ProcessMap(MapInfo map)
		{
			// new map
			if (map != null)
			{
				if (m_Engine != null)
				{
					GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
					GL.Flush();
					Refresh();

					m_Engine.Delete();
				}

				static_theEngine = null;
				static_theMap = null;

				m_Engine = null;

				m_Engine = new Player(m_openGLControl, m_SoundManager);
				m_Observer.Subscribe(m_Engine);

				BackgroundWorker bw = new BackgroundWorker();
				bw.DoWork += workerthread_LoadMap;
				bw.RunWorkerCompleted += mainthread_LoadMapCompleted;

				static_theEngine = m_Engine;
				static_theMap = map;

				m_openGLControl.Visible = false;
				m_controlMapProgress.Visible = true;

				m_openGLControl.Context.MakeCurrent(null);
				bw.RunWorkerAsync();

				SetViewMode(false);
				LOGGER.Info("About to call Begin() on map progress form");
				m_controlMapProgress.Begin_MainThread();
				LOGGER.Info("Finished with map progress begin");

				if(map.CollisionDetection == false)
				{
					SwitchToGhost(); // start as ghost so you don't fall through the ground immediately
				}
			}
			// revert to old map
			else if (map == null && m_Engine != null && m_Engine.NumStaticFigs > 0)
			{
				SetCursor(false, false);

				resetMouseCursor();

				// start timer for opengl refreshing
				SetRunning(true);
				StartStopRedrawer(true);
                m_swDelayMusicStart.Start();
			}
			// no map currently chosen
			else
			{
				SetCursor(false, false);
				WriteOpenMapMessage();
			}

			m_bOpeningMap = false;
		}

		private void WriteOpenMapMessage()
		{
			GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
			//m_fonter.PrintGLUTCenter("Press 'O' to open a map", m_openGLControl.Width, m_openGLControl.Height, 0);
			m_fonter.PrintCenter("Press 'O' to open a map", m_openGLControl.Width, m_openGLControl.Height, 0);
			//m_fonter.PrintTopCenter("Press 'O' to open a map", m_openGLControl.Width, m_openGLControl.Height, 0);
			GL.Flush();
			Refresh();
		}

		/// <summary>
		/// Get pointer to current opengl control window
		/// </summary>
		public OpenGLControlModded.simpleOpenGlControlEx OpenGLControl
		{
			get { return m_openGLControl; }
		}

		private void DisableDebuggingMode()
		{
			if (STATE.DebuggingMode)
			{
				STATE.DebuggingMode = false;
			}
		}

		// Set the MovableCamera mouse coordinate to new mouse position
		// show scene after every mouse movement
		private void openGLControl_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
		{
			if (m_bRunning)
			{
				m_Engine.MouseMove(e, ref m_bPostOpen);
			}
		}			

		// Centers this cursor to middle of openGlControl
		private void resetMouseCursor()
		{
			m_CursorPoint.X = Location.X + Width / 2;
			m_CursorPoint.Y = Location.Y + Height / 2;
			Cursor.Position = m_CursorPoint;
		}

		private void m_openGLControl_GotFocus(Object sender, EventArgs e)
		{
			if (!m_bOpeningMap && m_bPaused && !m_bLoadingMap && !m_bClosed && m_Engine != null)
			{
				m_bPaused = false;
				SetRunning(true);

				StartStopRedrawer(true);
			}
			else if (m_Engine == null)
			{
				WriteOpenMapMessage();
			}
		}

		private void m_openGLControl_LostFocus(object sender, System.EventArgs e)
		{
			if (!m_bOpeningMap && !m_bPaused && !m_bLoadingMap && !m_bClosed && m_Engine != null)
			{
				m_bPaused = true;
				SetRunning(false);
				StopAllTimers();
			}
		}
	}
}
