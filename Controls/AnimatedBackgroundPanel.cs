using System.Drawing.Drawing2D;

namespace HardwareSerialChecker.Controls;

public class AnimatedBackgroundPanel : Panel
{
    private List<Particle> particles;
    private System.Windows.Forms.Timer animationTimer;
    private Random random;
    private const int ParticleCount = 75;
    private bool seededOnce = false;

    public AnimatedBackgroundPanel()
    {
        this.DoubleBuffered = true;
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        
        random = new Random();
        particles = new List<Particle>();
        
        // Initialize particles (use safe bounds even if control hasn't been sized yet)
        for (int i = 0; i < ParticleCount; i++)
        {
            particles.Add(new Particle
            {
                X = random.Next(0, Math.Max(1, this.Width)),
                Y = random.Next(0, Math.Max(1, this.Height)),
                Z = random.NextDouble() * 500 + 100,
                VelocityX = (random.NextDouble() - 0.5) * 2,
                VelocityY = (random.NextDouble() - 0.5) * 2,
                VelocityZ = (random.NextDouble() - 0.5) * 3
            });
        }
        
        // Animation timer
        animationTimer = new System.Windows.Forms.Timer();
        animationTimer.Interval = 16; // ~60 FPS
        animationTimer.Tick += AnimationTimer_Tick;
        animationTimer.Start();
        
        this.Resize += (s, e) =>
        {
            if (!seededOnce && this.Width > 0 && this.Height > 0)
                SeedParticlesPositions();
            else
                ResetParticles();
        };
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        if (!seededOnce && this.Width > 0 && this.Height > 0)
            SeedParticlesPositions();
    }

    private void SeedParticlesPositions()
    {
        foreach (var p in particles)
        {
            p.X = random.Next(0, Math.Max(1, this.Width));
            p.Y = random.Next(0, Math.Max(1, this.Height));
        }
        seededOnce = true;
    }

    private void ResetParticles()
    {
        foreach (var particle in particles)
        {
            if (particle.X < 0 || particle.X > this.Width)
                particle.X = random.Next(0, this.Width);
            if (particle.Y < 0 || particle.Y > this.Height)
                particle.Y = random.Next(0, this.Height);
        }
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        // Update particle positions
        foreach (var particle in particles)
        {
            particle.X += particle.VelocityX;
            particle.Y += particle.VelocityY;
            particle.Z += particle.VelocityZ;
            
            // Wrap around edges
            if (particle.X < -50) particle.X = this.Width + 50;
            if (particle.X > this.Width + 50) particle.X = -50;
            if (particle.Y < -50) particle.Y = this.Height + 50;
            if (particle.Y > this.Height + 50) particle.Y = -50;
            
            // Oscillate Z for 3D effect
            if (particle.Z < 100 || particle.Z > 600)
                particle.VelocityZ = -particle.VelocityZ;
        }
        
        this.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Black);
        
        // Draw particles with 3D perspective
        foreach (var particle in particles)
        {
            // Calculate size based on Z depth
            float scale = 600f / (float)particle.Z;
            float size = 8 * scale;
            
            using (var brush = new SolidBrush(Color.Maroon))
            {
                g.FillEllipse(brush, 
                    (float)particle.X - size / 2, 
                    (float)particle.Y - size / 2, 
                    size, 
                    size);
            }
        }
        
        // Draw connection lines between nearby particles
        for (int i = 0; i < particles.Count; i++)
        {
            for (int j = i + 1; j < particles.Count; j++)
            {
                double dx = particles[i].X - particles[j].X;
                double dy = particles[i].Y - particles[j].Y;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                
                if (distance < 150)
                {
                    int alpha = (int)((1 - distance / 150) * 60);
                    using (var pen = new Pen(Color.FromArgb(alpha, 128, 0, 0), 1))
                    {
                        g.DrawLine(pen, 
                            (float)particles[i].X, 
                            (float)particles[i].Y, 
                            (float)particles[j].X, 
                            (float)particles[j].Y);
                    }
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            animationTimer?.Stop();
            animationTimer?.Dispose();
        }
        base.Dispose(disposing);
    }

    private class Particle
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public double VelocityZ { get; set; }
    }
}
