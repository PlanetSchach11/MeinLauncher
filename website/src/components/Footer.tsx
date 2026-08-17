export default function Footer() {
  const year = new Date().getFullYear();

  return (
    <footer className="footer">
      <div className="container">
        <div className="footer-inner">
          <div className="footer-brand">
            <div className="footer-brand-name">
              <span
                className="nav-brand-icon"
                aria-hidden="true"
                style={{ width: '28px', height: '28px', fontSize: '0.8rem', borderRadius: '8px' }}
              >
                K
              </span>
              Kulka Client
            </div>
            <p className="footer-brand-desc">
              Dein moderner Minecraft Launcher für Windows. Kostenlos.
            </p>
          </div>

          <div>
            <div className="footer-column-title">Navigation</div>
            <div className="footer-links">
              <a href="#home" className="footer-link">Home</a>
              <a href="#features" className="footer-link">Features</a>
              <a href="#download" className="footer-link">Download</a>
              <a href="#changelog" className="footer-link">Changelog</a>
            </div>
          </div>

          <div>
            <div className="footer-column-title">Projekt</div>
            <div className="footer-links">
              <a href="#" className="footer-link">GitHub</a>
              <a href="#news" className="footer-link">News</a>
              <a href="#support" className="footer-link">Support</a>
              <a href="#" className="footer-link">Lizenz</a>
            </div>
          </div>

          <div>
            <div className="footer-column-title">Rechtliches</div>
            <div className="footer-links">
              <a href="#" className="footer-link">Impressum</a>
              <a href="#" className="footer-link">Datenschutz</a>
            </div>
          </div>
        </div>

        <div className="footer-bottom">
          <span>&copy; {year} Kulka Client. Alle Rechte vorbehalten.</span>
          <span>Made with care.</span>
        </div>
      </div>
    </footer>
  );
}
