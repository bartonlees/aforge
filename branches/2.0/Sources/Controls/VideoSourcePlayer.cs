﻿// AForge Controls Library
// AForge.NET framework
//
// Copyright © Andrew Kirillov, 2005-2008
// andrew.kirillov@gmail.com
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using AForge.Video;

namespace AForge.Controls
{
    /// <summary>
    /// Video source player control.
    /// </summary>
    /// 
    /// <remarks><para>The control is aimed to play video sources, which implement
    /// <see cref="AForge.Video.IVideoSource"/> interface. To start playing a video
    /// the <see cref="VideoSource"/> property should be initialized first and then
    /// <see cref="Start"/> method should be called.</para></remarks>
    /// 
    public partial class VideoSourcePlayer : Control
    {
        // video source to play
        private IVideoSource videoSource = null;
        // last received frame from the video source
        private Bitmap currentFrame = null;
        // last error message provided by video source
        private string lastErrorMessage = null;
        // controls border color
        private Color borderColor = Color.Black;

        private bool autosize = true;
        private bool needSizeUpdate = false;
        private bool firstFrameNotProcessed = true;

        // parent of the control
        private Control parent = null;

        /// <summary>
        /// Auto size control or not.
        /// </summary>
        /// 
        /// <remarks><para>The property specifies if the control should be autosized or not.
        /// If the property is set to <b>true</b>, then the control will change its size according to
        /// video size and control will change its position automatically to be in the center
        /// of parent's control.
        /// </para></remarks>
        /// 
        [DefaultValue( true )]
        public bool AutoSizeControl
        {
            get { return autosize; }
            set
            {
                autosize = value;
                UpdatePosition( );
            }
        }

        /// <summary>
        /// Control's border color.
        /// </summary>
        /// 
        /// <remarks><para>Specifies color of the border drawn around video frame.</para></remarks>
        /// 
        [DefaultValue( typeof(Color), "Black" )]
        public Color BorderColor
        {
            get { return borderColor; }
            set
            {
                borderColor = value;
                Invalidate( );
            }
        }

        /// <summary>
        /// Video source to play.
        /// </summary>
        /// 
        /// <remarks><para>The property sets the video source to play. After setting the property the
        /// <see cref="Start"/> method should be used to start playing the video source.</para>
        /// 
        /// <para><note>Trying to change video source while currently set video source is still playing
        /// will generate an exception. Use <see cref="IsRunning"/> property to check if current video
        /// source is still playing or <see cref="Stop"/> or <see cref="SignalToStop"/> and <see cref="WaitForStop"/>
        /// methods to stop current video source.</note></para>
        /// </remarks>
        /// 
        /// <exception cref="Exception">Video source can not be changed while current video source is still running.</exception>
        /// 
        [Browsable( false )]
        public IVideoSource VideoSource
        {
            get { return videoSource; }
            set
            {
                lock ( this )
                {
                    // detach events
                    if ( videoSource != null )
                    {
                        if ( videoSource.IsRunning )
                        {
                            throw new Exception( "Can not change video source while current is running." );
                        }
                        videoSource.NewFrame -= new NewFrameEventHandler( videoSource_NewFrame );
                        videoSource.VideoSourceError -= new VideoSourceErrorEventHandler( videoSource_VideoSourceError );
                    }

                    videoSource = value;

                    // atach events
                    if ( videoSource != null )
                    {
                        videoSource.NewFrame += new NewFrameEventHandler( videoSource_NewFrame );
                        videoSource.VideoSourceError += new VideoSourceErrorEventHandler( videoSource_VideoSourceError );
                    }

                    needSizeUpdate = true;
                    firstFrameNotProcessed = true;
                    // update the control
                    Invalidate( );
                }
            }
        }

        /// <summary>
        /// State of the current video source.
        /// </summary>
        /// 
        /// <remarks><para>Current state of the current video source object - running or not.</para></remarks>
        /// 
        [Browsable( false )]
        public bool IsRunning
        {
            get
            {
                lock ( this )
                {
                    return ( videoSource != null ) ? videoSource.IsRunning : false;
                }
            }
        }

        /// <summary>
        /// Delegate to notify about new frame.
        /// </summary>
        /// 
        /// <param name="sender">Event sender.</param>
        /// <param name="image">New frame.</param>
        /// 
        public delegate void NewFrameHandler( object sender, ref Bitmap image );

        /// <summary>
        /// New frame event.
        /// </summary>
        /// 
        /// <remarks><para>The event is fired on each new frame received from video source. The
        /// event is fired right after receiving and before displaying, what gives a chance to
        /// user to perform some image processing on the new frame and/or update it.
        /// </para></remarks>
        /// 
        public event NewFrameHandler NewFrame;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoSourcePlayer"/> class.
        /// </summary>
        public VideoSourcePlayer( )
        {
            InitializeComponent( );

            // update control style
            SetStyle( ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw |
                ControlStyles.DoubleBuffer | ControlStyles.UserPaint, true );
        }
        
        /// <summary>
        /// Start video source and displaying its frames.
        /// </summary>
        public void Start( )
        {
            lock ( this )
            {
                firstFrameNotProcessed = true;

                videoSource.Start( );
                Invalidate( );
            }
        }

        /// <summary>
        /// Stop video source.
        /// </summary>
        /// 
        /// <remarks><para>The method stops video source by calling its <see cref="AForge.Video.IVideoSource.Stop"/>
        /// method, which abourts internal video source's thread. Use <see cref="SignalToStop"/> and
        /// <see cref="WaitForStop"/> for more polite video source stopping, which gives a chance for
        /// video source to perform proper shut down and clean up.
        /// </para></remarks>
        /// 
        public void Stop( )
        {
            lock ( this )
            {
                videoSource.Stop( );

                if ( currentFrame != null )
                {
                    currentFrame.Dispose( );
                    currentFrame = null;
                }
                Invalidate( );
            }
        }

        /// <summary>
        /// Signal video source to stop. 
        /// </summary>
        /// 
        /// <remarks><para>Use <see cref="WaitForStop"/> method to wait until video source
        /// stops.</para></remarks>
        /// 
        public void SignalToStop( )
        {
            lock ( this )
            {
                videoSource.SignalToStop( );
            }
        }

        /// <summary>
        /// Wait for video source has stopped. 
        /// </summary>
        /// 
        /// <remarks><para>Waits for video source stopping after it was signalled to stop using
        /// <see cref="SignalToStop"/> method.</para></remarks>
        /// 
        public void WaitForStop( )
        {
            lock ( this )
            {
                videoSource.WaitForStop( );

                if ( currentFrame != null )
                {
                    currentFrame.Dispose( );
                    currentFrame = null;
                }
                Invalidate( );
            }
        }

        // Paing control
        private void VideoSourcePlayer_Paint( object sender, PaintEventArgs e )
        {
            // is it required to update control's size/position
            if ( ( needSizeUpdate ) || ( firstFrameNotProcessed ) )
            {
                UpdatePosition( );
                needSizeUpdate = false;
            }

            lock ( this )
            {
                Graphics  g = e.Graphics;
                Rectangle rect = this.ClientRectangle;
                Pen       borderPen = new Pen( borderColor, 1 );

                // draw rectangle
                g.DrawRectangle( borderPen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1 );

                if ( videoSource != null )
                {
                    if ( ( currentFrame != null ) && ( lastErrorMessage == null ) )
                    {
                        // draw current frame
                        g.DrawImage( currentFrame, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2 );
                        firstFrameNotProcessed = false;
                    }
                    else
                    {
                        // display status string only in the case if video source is runnning
                        if ( videoSource.IsRunning )
                        {
                            // create font and brush
                            SolidBrush drawBrush = new SolidBrush( this.ForeColor );

                            g.DrawString( ( lastErrorMessage == null ) ? "Connecting ..." : lastErrorMessage,
                                this.Font, drawBrush, new PointF( 5, 5 ) );

                            drawBrush.Dispose( );
                        }
                    }
                }

                borderPen.Dispose( );
            }
        }

        // Update controls size and position
        private void UpdatePosition( )
        {
            lock ( this )
            {
                if ( ( autosize ) && ( this.Parent != null ) )
                {
                    Rectangle rc = this.Parent.ClientRectangle;
                    int width = 320;
                    int height = 240;

                    if ( currentFrame != null )
                    {
                        // get frame size
                        width  = currentFrame.Width;
                        height = currentFrame.Height;
                    }

                    // update controls size and location
                    this.SuspendLayout( );
                    this.Location = new Point( ( rc.Width - width - 2 ) / 2, ( rc.Height - height - 2 ) / 2 );
                    this.Size = new Size( width + 2, height + 2 );
                    this.ResumeLayout( );
                }
            }
        }

        // On new frame ready
        private void videoSource_NewFrame( object sender, NewFrameEventArgs eventArgs )
        {
            lock ( this )
            {
                // dispose previous frame
                if ( currentFrame != null )
                {
                    currentFrame.Dispose( );
                    currentFrame = null;
                }

                currentFrame = (Bitmap) eventArgs.Frame.Clone( );
                lastErrorMessage = null;

                // notify about the new frame
                if ( NewFrame != null )
                {
                    NewFrame( this, ref currentFrame );
                }

                // update control
                Invalidate( );
            }
        }

        // Error occured in video source
        private void videoSource_VideoSourceError( object sender, VideoSourceErrorEventArgs eventArgs )
        {
            lastErrorMessage = eventArgs.Description;
        }

        // Parent Changed event handler
        private void VideoSourcePlayer_ParentChanged( object sender, EventArgs e )
        {
            if ( autosize )
            {
                if ( parent != null )
                {
                    parent.SizeChanged -= new EventHandler( parent_SizeChanged );
                }

                parent = this.Parent;

                // set handler for Size Changed parent's event
                if ( parent != null )
                {
                    parent.SizeChanged += new EventHandler( parent_SizeChanged );
                }
            }
        }

        // Parent control has changed its size
        private void parent_SizeChanged( object sender, EventArgs e )
        {
            UpdatePosition( );
        }
    }
}