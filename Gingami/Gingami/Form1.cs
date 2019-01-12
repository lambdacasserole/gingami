using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;

namespace Gingami
{
    public partial class Form1 : Form
    {
        /// <summary>
        /// This is the backbuffer. We draw onto this first, then paint this onto the form.
        /// </summary>
        private readonly Bitmap _backbuffer;

        /// <summary>
        /// This is the graphics for interacting with the backbuffer.
        /// </summary>
        private readonly Graphics _graphics;

        /// <summary>
        /// This is the actual drawing surface of the form we copy our backbuffer onto.
        /// </summary>
        private readonly Graphics _surface;

        /// <summary>
        /// Set to true to enable antialiasing (set in form constructor).
        /// </summary>
        private readonly bool _antialiasing;

        /// <summary>
        /// The size of the viewport (set in form constructor).
        /// </summary>
        private readonly Size _displaySize;

        /// <summary>
        /// The background thread that handles rendering.
        /// </summary>
        private Thread _renderThread;

        /// <summary>
        /// Whether or not to show the frames per second counter.
        /// </summary>
        private readonly bool _showFps;

        /// <summary>
        /// The paint event that shadows the form's.
        /// </summary>
        public new event PaintEventHandler Paint;

        /// <summary>
        /// The paint event handler that shadows the form's.
        /// </summary>
        public delegate void PaintEventHandler(Graphics g);

        /// <summary>
        /// Holds ticks for the frames per second counter.
        /// </summary>
        private readonly Queue<long> _fpsTicks;

        // TODO: Example code. Tracks ball count, position, size and x/y velocity.
        private int _ballCount;
        private int[] _dx;
        private int[] _dy;
        private int[] _x;
        private int[] _y;
        private int[] _w;
        private int[] _h;

        public Form1()
        {
            InitializeComponent();

            // Initialize queue for the frames per second counter.
            _showFps = true;
            _fpsTicks = new Queue<long>();

            // Set some configuration options, including the size of the viewport and antialiasing setting.
            _displaySize = new Size(800, 600);
            _antialiasing = true;

            // Fix size of form.
            ClientSize = _displaySize;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            SetStyle(ControlStyles.FixedHeight, true);
            SetStyle(ControlStyles.FixedWidth, true);

            // Initialize backbuffer and corresponding graphics object.
            _backbuffer = new Bitmap(ClientSize.Width, ClientSize.Height);
            _graphics = Graphics.FromImage(_backbuffer);
            
            // Now, let's configure the graphics object pointing at the backbuffer for decent quality.
            if (_antialiasing)
            {
                _graphics.SmoothingMode = SmoothingMode.AntiAlias;
                _graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            }
            _graphics.CompositingMode = CompositingMode.SourceOver;
            _graphics.CompositingQuality = CompositingQuality.HighSpeed;
            _graphics.InterpolationMode = InterpolationMode.Low;
            _graphics.PixelOffsetMode = PixelOffsetMode.Half;

            // We don't need any special compositing etc. for the surface of our form, because everything has been rendered on the backbuffer already.
            _surface = CreateGraphics();
            _surface.CompositingMode = CompositingMode.SourceCopy;
            _surface.CompositingQuality = CompositingQuality.AssumeLinear;
            _surface.SmoothingMode = SmoothingMode.None;
            _surface.InterpolationMode = InterpolationMode.NearestNeighbor;
            _surface.TextRenderingHint = TextRenderingHint.SystemDefault;
            _surface.PixelOffsetMode = PixelOffsetMode.HighSpeed;

            // Optimize window painting for our purposes.
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.UserPaint, false);            
        }

        /// <summary>
        /// Contains the main game loop.
        /// </summary>
        private void GameLoop()
        {
            // TODO: Example code. Initial randomisation of balls. 
            _ballCount = 100;
            _x = new int[_ballCount];
            _y = new int[_ballCount];
            _w = new int[_ballCount];
            _h = new int[_ballCount];
            _dx = new int[_ballCount];
            _dy = new int[_ballCount];
            var r = new Random();
            for (var i = 0; i < _ballCount; i++)
            {
                _w[i] = r.Next(10, 100);
                _h[i] = _w[i];
                _x[i] = r.Next(0, _backbuffer.Width - _w[i]);
                _y[i] = r.Next(0, _backbuffer.Height - _h[i]);
                _dx[i] = r.Next(1, 10);
                _dy[i] = r.Next(1, 10);
            }

            // This is the main game loop.
            while (!Disposing && !IsDisposed && Visible)
            {
                try
                {
                    // TODO: Example code. Movement of balls (and velocity inversion).
                    for (int i = 0; i < _ballCount; i++)
                    {
                        _x[i] += _dx[i];
                        _y[i] += _dy[i];

                        if (_x[i] >= _backbuffer.Width - _w[i])
                        {
                            _x[i] = _backbuffer.Width - _w[i];
                            _dx[i] *= -1;
                        }

                        if (_y[i] >= _backbuffer.Height - _h[i])
                        {
                            _y[i] = _backbuffer.Height - _h[i];
                            _dy[i] *= -1;
                        }

                        if (_x[i] <= 0)
                        {
                            _x[i] = 0;
                            _dx[i] *= -1;
                        }

                        if (_y[i] <= 0)
                        {
                            _y[i] = 0;
                            _dy[i] *= -1;
                        }
                    }
                    
                    // Keep frames per second counter up-to-date.
                    _fpsTicks.Enqueue(DateTime.Now.Ticks);
                    while (DateTime.Now.Ticks - 10000000 > _fpsTicks.Peek())
                    {
                        _fpsTicks.Dequeue();
                    }

                    // Raise paint event (and draw buffered image all at once.
                    Paint(_graphics);
                    _surface.DrawImageUnscaled(_backbuffer, 0, 0);
                }
                catch (Exception e)
                {
                    // Print exception details in case of error.
                    Debug.Print(e.ToString());
                }
            }

            // Some cleanup.
            _surface.Dispose();
            _graphics.Dispose();
            _backbuffer.Dispose();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Start thread with game loop in it.
            _renderThread = new Thread(GameLoop);
            if (!_renderThread.TrySetApartmentState(ApartmentState.MTA))
            {
                Debug.Print("Can't start in multi-threaded mode.");
            }
            _renderThread.Start();
        }

        private void Form1_Paint(Graphics g)
        {
            // TODO: Example code. Draw balls.
            g.Clear(Color.DeepSkyBlue);
            for (var i = 0; i < _ballCount; i++)
            {
                g.FillEllipse(Brushes.AliceBlue, _x[i], _y[i], _w[i], _h[i]);
            }

            // Draw frames per second.
            if (_showFps)
            {
                g.DrawString(_fpsTicks.Count.ToString(), new Font("Consolas", 48), new SolidBrush(Color.FromArgb(128, Color.Black)), 0, 0);
            }
        }
    }
}
