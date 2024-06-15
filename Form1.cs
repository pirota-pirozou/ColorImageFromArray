using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace ColorImageFromArray
{
    public partial class Form1 : Form
    {
        private int width = 256;
        private int height = 256;
        private int[] pixelData;
        private Bitmap? bitmap;
        private CachedBitmap? cachedBitmap;

        private Stopwatch? stopwatch;

        // �}���`�X���b�h�쓮
        private CancellationTokenSource? cts;
        private Task? task;

        private bool isRunning = false;

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            pixelData = new int[width * height];
        }

        /// <summary>
        /// �t�H�[�������[�h���ꂽ�Ƃ�
        /// </summary>
        private void Form1_Load(object sender, EventArgs e)
        {
            // ���l�z���RGB�f�[�^��ݒ�i��: �J���[�O���f�[�V�����j
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int r = 255 * x / width;
                    int g = 255 * y / height;
                    int b = 255 * (x + y) / (width + height);
                    pixelData[y * width + x] = (255 << 24) | (r << 16) | (g << 8) | b;
                }
            }

            // �r�b�g�}�b�v���쐬���A���l�z����R�s�[
            bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            // Stopwatch�N���X����
            stopwatch = new Stopwatch();
            stopwatch.Start();

            // �^�C�}�[�ݒ�
            SetupTimer();
        }

        /// <summary>
        /// �񓯊��Ńt���[���X�V�^�C�}�[��ݒ�
        /// </summary>
        private void SetupTimer()
        {
            cts = new CancellationTokenSource();

            isRunning = true;
            // �^�X�N����
            task = Task.Run(async () =>
            {
                while (isRunning)
                {
                    // �^�X�N���L�����Z������Ă��Ȃ����`�F�b�N
                    if (cts.Token.IsCancellationRequested)
                    {
                        Debug.WriteLine("Task is cancelled.");
                        break;
                    }
                    await Task.Delay(0);
                    // ������ 17ms ���ƂɎ��s�������R�[�h���L�q���܂�
                    var ts = stopwatch?.Elapsed;
                    if (ts?.Milliseconds >= 17)
                    {
                        Debug.WriteLine("RunningLoop " + ts?.Milliseconds);
                        stopwatch?.Restart();
                        UpdateFrame(); // �t���[���X�V
                        this.Invalidate(); // �ĕ`��
                    }
                }
            }, cts.Token);
        }

        /// <summary>
        /// �\��
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (!isRunning) return;

            // �r�b�g�}�b�v��\��
            if (cachedBitmap != null)
            {
                g.DrawCachedBitmap(cachedBitmap, 0, 0);
            }
        }

        /// <summary>
        /// �t���[���X�V����
        /// </summary>
        private void UpdateFrame()
        {
            if (bitmap == null) return;

            // ���l�z���RGB�f�[�^��ݒ�i��: �J���[�O���f�[�V�����j
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData? bmpData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(pixelData, 0, bmpData.Scan0, pixelData.Length);
            bitmap.UnlockBits(bmpData);
            // CachedBitmap���X�V
            using (Graphics g = this.CreateGraphics())
            {
                cachedBitmap = new CachedBitmap(bitmap, g);
            }
            bmpData = null;
            GC.Collect();
        }

        /// <summary>
        /// �t�H�[�������
        /// </summary>
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            //
            cts?.Cancel();
            isRunning = false;

            // �^�X�N���I������܂őҋ@
            task?.Wait();
            task?.Dispose();
            task = null;
            stopwatch?.Stop();
            stopwatch = null;

            // �L�����Z���g�[�N�������
            cts?.Dispose();
            cts = null;

            // �A�C�h����ԃC�x���g����
            cts?.Dispose();
            cts = null;

            // cachedBitmap���
            cachedBitmap?.Dispose();
            cachedBitmap = null;

            // bitmap���
            bitmap?.Dispose();
            bitmap = null;

            GC.Collect();
        }

    }
}
