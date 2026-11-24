import { mkdir, readFile, writeFile } from 'node:fs/promises';

const read = name => readFile(new URL(name, import.meta.url), 'utf8');
const [logic, activity, connection, worker, html, css, client, charts] = await Promise.all(
  ['teamers.mjs', 'activity.mjs', 'connection.mjs', 'worker.mjs', 'teamers.html', 'styles.css', 'client.js', 'charts.js'].map(read)
);
const assets = JSON.stringify({ html, css, client, charts });
const output = new URL('dist/server/', import.meta.url);
await mkdir(output, { recursive: true });
await writeFile(new URL('index.js', output), `const assets = ${assets};\n${logic}\n${activity}\n${connection}\n${worker}`);
