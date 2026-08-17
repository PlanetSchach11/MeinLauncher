import Navigation from './components/Navigation';
import Hero from './components/Hero';
import Preview from './components/Preview';
import Features from './components/Features';
import AllInOne from './components/AllInOne';
import Updates from './components/Updates';
import Download from './components/Download';
import News from './components/News';
import Changelog from './components/Changelog';
import Support from './components/Support';
import Footer from './components/Footer';

export default function App() {
  return (
    <>
      <Navigation />
      <main>
        <Hero />
        <Preview />
        <Features />
        <AllInOne />
        <Updates />
        <Download />
        <News />
        <Changelog />
        <Support />
      </main>
      <Footer />
    </>
  );
}
