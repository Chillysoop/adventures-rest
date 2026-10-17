// Bundles the browser client into one self-contained HTML file (dist/crown-and-compass.html),
// which is what gets published as the playable build.

import { build } from 'esbuild';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';

const result = await build({
  entryPoints: ['src/client/main.ts'],
  bundle: true,
  minify: true,
  format: 'iife',
  target: 'es2022',
  write: false,
  legalComments: 'none',
});
const js = result.outputFiles[0].text.replace(/<\/script/gi, '<\\/script');
const html = readFileSync('src/client/index.html', 'utf8').replace('/*__APP__*/', () => js);
mkdirSync('dist', { recursive: true });
writeFileSync('dist/crown-and-compass.html', html);
console.log(`dist/crown-and-compass.html  ${(html.length / 1024).toFixed(0)} KB`);
