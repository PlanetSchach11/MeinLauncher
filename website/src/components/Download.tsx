import { useInView } from '../hooks/useInView';

export default function Download() {
  const { ref, isVisible } = useInView({ threshold: 0.15 });

  return (
    <section className="section download-section" id="download">
      <div className="container" ref={ref}>
        <div className={`reveal${isVisible ? ' visible' : ''}`}>
          <span className="section-label" style={{ justifyContent: 'center' }}>Download</span>
          <h2 className="section-title" style={{ textAlign: 'center' }}>
            Kulka Client für Windows
          </h2>
        </div>

        <div className={`download-card card reveal${isVisible ? ' visible' : ''}`} style={{ transitionDelay: '150ms' }}>
          <div className="download-version">
            v0.2.0
          </div>

          <p style={{ color: 'var(--text-secondary)', marginBottom: '32px', lineHeight: '1.7' }}>
            Der Kulka Client ist ein kostenloser Minecraft Launcher für Windows.
            Lade die neueste Version herunter und starte in Sekunden.
          </p>

          <a
            href="https://github.com/PlanetSchach11/MeinLauncher/releases/download/v0.2.0/KulkaClient-v0.1.0-win-x64.zip"
            className="btn btn-primary"
            style={{ fontSize: '1.0625rem', padding: '16px 40px' }}
          >
            <span aria-hidden="true">⬇</span>
            Kulka Client herunterladen
          </a>

          <div className="download-specs">
            <span className="download-spec">Windows 10 / 11</span>
            <span className="download-spec">x64</span>
            <span className="download-spec">ca. 71 MB</span>
            <span className="download-spec">Kostenlos</span>
          </div>

          <p className="download-note">
            Direkt von GitHub heruntergeladen – keine Registry, kein Installer.
          </p>
        </div>

        <div
          className={`reveal${isVisible ? ' visible' : ''}`}
          style={{ transitionDelay: '300ms', marginTop: '48px' }}
        >
          <h3 style={{ fontSize: '1.125rem', fontWeight: '600', marginBottom: '16px', textAlign: 'center' }}>
            Systemanforderungen
          </h3>
          <div style={{ display: 'flex', justifyContent: 'center', gap: '32px', flexWrap: 'wrap' }}>
            <Spec label="Betriebssystem" value="Windows 10 / 11 (64-bit)" />
            <Spec label="RAM" value="mindestens 4 GB" />
            <Spec label="Java" value="wird automatisch erkannt" />
            <Spec label="Festplattenplatz" value="ca. 100 MB + Minecraft" />
          </div>
        </div>
      </div>
    </section>
  );
}

function Spec({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ textAlign: 'center' }}>
      <div style={{ fontSize: '0.75rem', color: 'var(--text-tertiary)', textTransform: 'uppercase', letterSpacing: '0.05em', marginBottom: '4px' }}>
        {label}
      </div>
      <div style={{ fontSize: '0.9375rem', color: 'var(--text-primary)', fontWeight: '500' }}>
        {value}
      </div>
    </div>
  );
}
