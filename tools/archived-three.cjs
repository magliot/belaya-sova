// Offline asset exporters and save-migration checks read the exact approved archive.
const {execFileSync} = require('node:child_process');
const path = require('node:path');
module.exports = function archivedFile(name) {
  return execFileSync('git', ['show', 'archive/three-local-20261120:docs/' + name], {
    cwd: path.resolve(__dirname, '..'), encoding: 'utf8', maxBuffer: 12 * 1024 * 1024
  });
};
