// Zoran Zoltan Kurešević
//email: msz.kuresevic@gmail.com

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Media;
using System.Runtime.InteropServices;

namespace Metronome
{
    /// <summary>
    /// ***************************
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int MAX_BPM = 500;
        private const int MIN_BPM = 1;
        private Multimedia.Timer _timer;
       // private TweenMotionLib.TweenMotion motion;
        private System.Threading.Thread tickThread;
        private System.Windows.Media.Effects.DropShadowEffect shadowEffect;
        private int bpm;
        //private string motionType;
        private SoundPlayer tikSound;
        private SoundPlayer tokSound;
        private float interval=1000;
        private int[] measureArray =new int[] { 2, 3, 4, 5, 6, 7, 8, 9 };
        private int measure=0;
        private int measureCount = 0;
        private bool isStart = false;
        private int tempoLineRotation=30;
        private bool soundOn = true;
        string divisor = "";
        
        public MainWindow()
        {          
            InitializeComponent();
            shadowEffect = new System.Windows.Media.Effects.DropShadowEffect();
           // motion = new TweenMotionLib.TweenMotion();
           // motion.onMotion += Motion_onMotion;
           // motion.TickInterval = 50;
           // motionType = TweenMotionLib.typeMotion.easeOutSine;
            _timer = new Multimedia.Timer();
            _timer.Period = 250;
            _timer.Tick += _timer_Tick;
            System.Threading.Thread thisThread =  System.Threading.Thread.CurrentThread;
            thisThread.Priority = System.Threading.ThreadPriority.AboveNormal;
            KeyDown += MainWindow_KeyDown;
            bpm = Properties.Settings.Default.bpm;
            measure = Properties.Settings.Default.measure;
            soundBtn.Focusable= startBtn.Focusable =stopBtn.Focusable=bpmIn.Focusable=bpmSub.Focusable=measureBtn.Focusable = false;
            bpmLabel.Content = Properties.Settings.Default.bpm.ToString();
            tikSound = new SoundPlayer(Properties.Resources.tik);
            tokSound = new SoundPlayer(Properties.Resources.tok);
            interval = 1000 * (60f / bpm);
            measureLabel.Content = measureArray[measure]+getDivisor();
            tempoLabel.Content = "-";
            bpmIn.ToolTip = "Key +";
            bpmSub.ToolTip= "Key -";
            tempoLine.Effect = getShadow(330); 
        }

        private void Motion_onMotion(double value, string type)
        {
            tempoLine.RenderTransform = new RotateTransform(value);
            tempoLine.Effect = getShadow( (int) value);
        }
        private System.Windows.Media.Effects.DropShadowEffect getShadow(int direction)
        {           
            shadowEffect.Direction = direction;
            shadowEffect.Opacity = .3;
            shadowEffect.ShadowDepth = 2;
            return shadowEffect;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch(e.Key.ToString())
            {
                case "Add":
                    bpmIn_Click(null, null);
                    break;
                case "Subtract":
                    bpmSub_Click(null, null);
                    break;
                case "Space":
                 if (isStart) { stopBtn_Click(null, null); }
                 else { startBtn_Click(null, null); }
                 break;
            }
        }
        
        private void _timer_Tick(object sender, EventArgs e)
        {
            if (measureCount >= measureArray[measure]) { measureCount = 0; }
            measureCount++;
            tickThread = new System.Threading.Thread(() => moveLabel());
            tickThread.Start();
            if (soundOn)
            {
              if (measureCount == 1) { tikSound.Play(); }
              else { tokSound.Play(); }
            }
        }
        private void moveLabel()
        {           
             Dispatcher.Invoke(new Action(() =>
             {
               tempoLabel.Content =  measureCount.ToString();
               tempoLabel.Margin = new Thickness { Top = 58, Left = 30 + measureCount * 10 };
              // motion.Start(motionType, 0, tempoLineRotation, (double)interval/1000);
               if (tempoLineRotation == 30) { tempoLineRotation *= -1; }
               else { tempoLineRotation = 30; }
             }));
          
        } 
        private void startBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!isStart)
            {                
                _timer.Period= (int) interval;
                _timer.Start();
                isStart = true;
                _timer_Tick(null, null);
            }
           
        }
        private void stopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (isStart)
            {
                _timer.Stop();
                isStart = false;
                measureCount = 0;
            }
        }       
        private void measureBtn_Click(object sender, RoutedEventArgs e)
        {
            measure++;
            if (measure >= 8) { measure = 0; }
            measureLabel.Content = measureArray[measure] + getDivisor(); 
            Properties.Settings.Default.measure = measure;
            saveSetting();
        }
        private void soundBtn_Click(object sender, RoutedEventArgs e)
        {
            soundOn = !soundOn;
            soundLabel.Content = soundOn ? "ZVUK ON" : "ZVUK OFF";
        }
        private string getDivisor()
        {           
            if (measure < 3) { divisor = "/4"; }
            else { divisor = "/8"; }
            return divisor;
        }
        private void bpmIn_Click(object sender, RoutedEventArgs e)
        {
            bpm++;
            if (bpm >= MAX_BPM) { bpm = MAX_BPM; }
            bpmLabel.Content = bpm.ToString();
            changeInterval();
        }
        private void bpmSub_Click(object sender, RoutedEventArgs e)
        {
            bpm--;
            if (bpm <= MIN_BPM) { bpm = MIN_BPM; }
            bpmLabel.Content = bpm.ToString();
            changeInterval();
        }
        private void changeInterval()
        {
            interval = 1000 * (60f / bpm);
            if (isStart)
            {
             _timer.Period = (int)interval;
            }
            Properties.Settings.Default.bpm = bpm;
            saveSetting();
        }
        private void saveSetting()
        {
            Properties.Settings.Default.Save();
        }       
    }
}
