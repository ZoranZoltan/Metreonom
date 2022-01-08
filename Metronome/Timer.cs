
#region Contact

/*
 * Zoran Zoltan Kurešević
 * Email: msz.kuresevic@gmail.com
 */

#endregion

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Multimedia
{
    /// <summary>
    /// Definiše konstante za tipove događaja multimedijskog timera.
    /// </summary>
    public enum TimerMode
    {
        /// <summary>
        /// Tajmer jednom.
        /// </summary>
        OneShot,

        /// <summary>
        /// Tajmer periodično
        /// </summary>
        Periodic
    };

    /// <summary>
    /// Informacije o mogućnostima tajmera
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TimerCaps
    {
        /// <summary>
        /// Minimalni razmak u milisekundama.
        /// </summary>
        public int periodMin;

        /// <summary>
        /// Maksimalni razmak u milisekundama
        /// </summary>
        public int periodMax;
    }

    /// <summary>
    /// Windows multimedijski tajmer.
    /// </summary>
    public sealed class Timer : IComponent
    {
        #region Timer Members

        #region Delegates

        // Metoda koju Windows poziva kada zove tajmer.
        private delegate void TimeProc(int id, int msg, int user, int param1, int param2);

        // Metode koje pozivaju događaje.
        private delegate void EventRaiser(EventArgs e);

        #endregion

        #region Win32 Multimedia Timer Functions

        // Mogućnosti tajmera.
        [DllImport("winmm.dll")]
        private static extern int timeGetDevCaps(ref TimerCaps caps,
            int sizeOfTimerCaps);

        // Kreira i startuje tajmer.
        [DllImport("winmm.dll")]
        private static extern int timeSetEvent(int delay, int resolution,
            TimeProc proc, int user, int mode);

        // Zaustavlja i briše tajmer.
        [DllImport("winmm.dll")]
        private static extern int timeKillEvent(int id);

        // Označava uspešnu operaciju.
        private const int TIMERR_NOERROR = 0;

        #endregion

        #region Fields

        // Identifikator tajmera.
        private int timerID;

        // Timer mod.
        private volatile TimerMode mode;

        // Period izmedju dogadjaja u milisekundama.
        private volatile int period;

        // Razlučivost tajmera u milisekundama.
        private volatile int resolution;        

        // Tajmer periodično.
        private TimeProc timeProcPeriodic;

        // Tajmer jednom.
        private TimeProc timeProcOneShot;

        // Predstavlja metodu koja podiže Tick događaj.
        private EventRaiser tickRaiser;

        // Indikator rada tajmera.
        private bool running = false;

        // Indikator pauziranja tajmera.
        private volatile bool disposed = false;

        // Objekat za rasporedjivanje dogadjaja.
        private ISynchronizeInvoke synchronizingObject = null;

        // Provera IComponent.
        private ISite site = null;

        // Mogućnosti tajmera.
        private static TimerCaps caps;

        #endregion

        #region Events

        /// <summary>
        /// Kada se startuje tajmer.
        /// </summary>
        public event EventHandler Started;

        /// <summary>
        /// Zaustavljanje tajmera.
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Proli vremenski period.
        /// </summary>
        public event EventHandler Tick;

        #endregion

        #region Construction

        /// <summary>
        /// Pokreće klasu.
        /// </summary>
        static Timer()
        {
            timeGetDevCaps(ref caps, Marshal.SizeOf(caps));
        }

        /// <summary>
        /// Pokreće novu instancu Timer klase s navedenim IContainer.
        /// </summary>
        /// <param name="container">
        /// IContainer na koji će Timer dodati sebe.
        /// </param>
        public Timer(IContainer container)
        {
            ///
            /// Klasa Windows.Form (Obavezna za podršku).
            ///
            container.Add(this);

            Initialize();
        }

        /// <summary>
        /// Nova instanca klase tajmer.
        /// </summary>
        public Timer()
        {
            Initialize();
        }

        ~Timer()
        {
            if(IsRunning)
            {
                // Zaustavlja i briše tajmer.
                timeKillEvent(timerID);
            }
        }

        // Inicijalizuje tajmer sa zadatim vrednostima.
        private void Initialize()
        {
            this.mode = TimerMode.Periodic;
            this.period = Capabilities.periodMin;
            this.resolution = 1;

            running = false;

            timeProcPeriodic = new TimeProc(TimerPeriodicEventCallback);
            timeProcOneShot = new TimeProc(TimerOneShotEventCallback);
            tickRaiser = new EventRaiser(OnTick);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Startovanje tajmera.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// tajmer je onemogućen.
        /// </exception>
        /// <exception cref="TimerStartException">
        /// Tajmer se nije pokrenuo.
        /// </exception>
        public void Start()
        {
            #region Require

            if(disposed)
            {
                throw new ObjectDisposedException("Timer");
            }

            #endregion

            #region Guard

            if(IsRunning)
            {
                return;
            }

            #endregion

            // Periodični povratni poziv.
            if (Mode == TimerMode.Periodic)
            {
                // Kreiraj i pokreni tajmer.
                timerID = timeSetEvent(Period, Resolution, timeProcPeriodic, 0, (int)Mode);
            }
            // Tajmer jednom.
            else
            {
                // Kreiraj i pokreni tajmer.
                timerID = timeSetEvent(Period, Resolution, timeProcOneShot, 0, (int)Mode);
            }

            // Ako je tajmer kreiran uspešno.
            if(timerID != 0)
            {
                running = true;

                if(SynchronizingObject != null && SynchronizingObject.InvokeRequired)
                {
                    SynchronizingObject.BeginInvoke(
                        new EventRaiser(OnStarted), 
                        new object[] { EventArgs.Empty });
                }
                else
                {
                    OnStarted(EventArgs.Empty);
                }                
            }
            else
            {
                throw new TimerStartException("Unable to start multimedia Timer.");
            }
        }

        /// <summary>
        /// Zaustavi tajmer.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Ako je tajmer već zaustavljen.
        /// </exception>
        public void Stop()
        {
            #region Require

            if(disposed)
            {
                throw new ObjectDisposedException("Timer");
            }

            #endregion

            #region Guard

            if(!running)
            {
                return;
            }

            #endregion

            // Zaustavi i obriši tajmer.
            int result = timeKillEvent(timerID);

            Debug.Assert(result == TIMERR_NOERROR);

            running = false;

            if(SynchronizingObject != null && SynchronizingObject.InvokeRequired)
            {
                SynchronizingObject.BeginInvoke(
                    new EventRaiser(OnStopped), 
                    new object[] { EventArgs.Empty });
            }
            else
            {
                OnStopped(EventArgs.Empty);
            }
        }        

        #region Callbacks

        // Callback method called by the Win32 multimedia timer when a timer
        // periodic event occurs.
        private void TimerPeriodicEventCallback(int id, int msg, int user, int param1, int param2)
        {
            if(synchronizingObject != null)
            {
                synchronizingObject.BeginInvoke(tickRaiser, new object[] { EventArgs.Empty });
            }
            else
            {
                OnTick(EventArgs.Empty);
            }
        }

        // Callback method called by the Win32 multimedia timer when a timer
        // one shot event occurs.
        private void TimerOneShotEventCallback(int id, int msg, int user, int param1, int param2)
        {
            if(synchronizingObject != null)
            {
                synchronizingObject.BeginInvoke(tickRaiser, new object[] { EventArgs.Empty });
                Stop();
            }
            else
            {
                OnTick(EventArgs.Empty);
                Stop();
            }
        }

        #endregion

        #region Event Raiser Methods

        // Raises the Disposed event.
        private void OnDisposed(EventArgs e)
        {
            EventHandler handler = Disposed;

            if(handler != null)
            {
                handler(this, e);
            }
        }

        // Raises the Started event.
        private void OnStarted(EventArgs e)
        {
            EventHandler handler = Started;

            if(handler != null)
            {
                handler(this, e);
            }
        }

        // Raises the Stopped event.
        private void OnStopped(EventArgs e)
        {
            EventHandler handler = Stopped;

            if(handler != null)
            {
                handler(this, e);
            }
        }

        // Raises the Tick event.
        private void OnTick(EventArgs e)
        {
            EventHandler handler = Tick;

            if(handler != null)
            {
                handler(this, e);
            }
        }

        #endregion        

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets poziva dogadjaja.
        /// </summary>
        public ISynchronizeInvoke SynchronizingObject
        {
            get
            {
                #region Require

                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }

                #endregion

                return synchronizingObject;
            }
            set
            {
                #region Require

                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }

                #endregion

                synchronizingObject = value;
            }
        }

        /// <summary>
        /// Gets or sets vreme izmedju tik-a.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Ako je tajmer već odložen.
        /// </exception>   
        public int Period
        {
            get
            {
                #region Require

                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }

                #endregion

                return period;
            }
            set
            {
                #region Require

                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }
                else if(value < Capabilities.periodMin || value > Capabilities.periodMax)
                {
                    throw new ArgumentOutOfRangeException("Period", value,
                        "Multimedia Timer period out of range.");
                }

                #endregion

                period = value;

                if(IsRunning)
                {
                    Stop();
                    Start();
                }
            }
        }

        /// <summary>
        /// Gets or sets razlučivost timera.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Ako je tajmer već odložen.
        /// </exception>        
        /// <remarks>
        /// ********************************
        /// **************************************
        /// *********************************
        /// *********************************** 
        /// ******************************************
        /// </remarks>
        public int Resolution
        {
            get
            {
                #region Require

                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }

                #endregion

                return resolution;
            }
            set
            {
                #region Require

                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }
                else if(value < 0)
                {
                    throw new ArgumentOutOfRangeException("Resolution", value,
                        "Multimedia timer resolution out of range.");
                }

                #endregion

                resolution = value;

                if(IsRunning)
                {
                    Stop();
                    Start();
                }
            }
        }

        /// <summary>
        /// Način rada tajmera.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Ako je tajmer već odložen.
        /// </exception>
        public TimerMode Mode
        {
            get
            {
                #region Require

                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }

                #endregion

                return mode;
            }
            set
            {
                #region Require

                if(disposed)
                {
                    throw new ObjectDisposedException("Timer");
                }

                #endregion
                
                mode = value;

                if(IsRunning)
                {
                    Stop();
                    Start();
                }
            }
        }

        /// <summary>
        /// Vrednosti koje se pokazuju kada je tajmer ukjlučen.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return running;
            }
        }

        /// <summary>
        /// Dobavlja mogućnosti tajmera.
        /// </summary>
        public static TimerCaps Capabilities
        {
            get
            {
                return caps;
            }
        }

        #endregion

        #endregion

        #region IComponent Members

        public event System.EventHandler Disposed;

        public ISite Site
        {
            get
            {
                return site;
            }
            set
            {
                site = value;
            }
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// FOslobadja rewsurse tajmera.
        /// </summary>
        public void Dispose()
        {
            #region Guard

            if(disposed)
            {
                return;
            }

            #endregion               

            if(IsRunning)
            {
                Stop();
            }

            disposed = true;

            OnDisposed(EventArgs.Empty);
        }

        #endregion       
    }

    /// <summary>
    /// Izuzetak koji se odbacuje kada se tajmer ne pokrene.
    /// </summary>
    public class TimerStartException : ApplicationException
    {
        /// <summary>
        /// Inicijalizuje novu instancu klase TimerStartException.
        /// </summary>
        /// <param name="message">
        /// Poruka o grešci. 
        /// </param>
        public TimerStartException(string message) : base(message)
        {
        }
    }
}
