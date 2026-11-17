// Local, deliberately simple rigs for the winter props. No Tripo processing credits.
// node tools/rig-winter.mjs dog|snowman|yeti input.glb output.glb
import { NodeIO } from '@gltf-transform/core';
import { transformMesh, prune } from '@gltf-transform/functions';

const [, , kind, input, output] = process.argv;
if (!['dog', 'snowman', 'yeti'].includes(kind) || !input || !output) {
  throw new Error('usage: node tools/rig-winter.mjs dog|snowman|yeti input.glb output.glb');
}
const io = new NodeIO();
const doc = await io.read(input), root = doc.getRoot();
const sourceNodes = root.listNodes().filter(n => n.getMesh());
if (sourceNodes.length !== 1 || sourceNodes[0].getMesh().listPrimitives().length !== 1) {
  throw new Error('Inspect this model: expected one static mesh and one primitive.');
}
if (root.listSkins().length || root.listAnimations().length) throw new Error('Already rigged.');
const mesh = sourceNodes[0].getMesh(), prim = mesh.listPrimitives()[0];
transformMesh(mesh, sourceNodes[0].getWorldMatrix());
// Tripo characters face +X; the game's actors face -Z.
transformMesh(mesh, [0, 0, -1, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1]);
const pos = prim.getAttribute('POSITION'), count = pos.getCount();
const min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
for (let i = 0; i < count; i++) {
  const v = pos.getElement(i, []);
  for (let j = 0; j < 3; j++) { min[j] = Math.min(min[j], v[j]); max[j] = Math.max(max[j], v[j]); }
}
const height = max[1] - min[1];
const center = [(max[0] + min[0]) / 2, min[1], (max[2] + min[2]) / 2];
for (let i = 0; i < count; i++) pos.setElement(i, pos.getElement(i, []).map((v, j) => (v - center[j]) / height));
const width = (max[0] - min[0]) / height, depth = (max[2] - min[2]) / height;
const rigRoot = doc.createNode('WinterRoot');
const specs = kind === 'dog'
  ? [[-width * .28, .37, -depth * .22], [width * .28, .37, -depth * .22],
     [-width * .28, .37, depth * .22], [width * .28, .37, depth * .22]]
  : [[kind === 'snowman' ? .21 : .24, kind === 'snowman' ? .55 : .67, 0],
     [kind === 'snowman' ? -.21 : -.24, kind === 'snowman' ? .55 : .67, 0]];
const joints = [rigRoot, ...specs.map((t, i) => {
  const n = doc.createNode(kind === 'dog' ? 'WinterLeg' + i : i === 0 ? 'WinterArmL' : 'WinterArmR').setTranslation(t);
  rigRoot.addChild(n); return n;
})];
const indices = new Uint16Array(count * 4), weights = new Float32Array(count * 4);
const smooth = t => { t = Math.max(0, Math.min(1, t)); return t * t * (3 - 2 * t); };
for (let i = 0; i < count; i++) {
  const [x, y, z] = pos.getElement(i, []);
  let joint, w;
  if (kind === 'dog') {
    joint = 1 + (x >= 0 ? 1 : 0) + (z >= 0 ? 2 : 0);
    w = smooth((.45 - y) / .18);
  } else {
    joint = x >= 0 ? 1 : 2;
    const cutoff = kind === 'snowman' ? .21 : .24;
    w = y > (kind === 'snowman' ? .34 : .30) && y < .83 ? smooth((Math.abs(x) - cutoff) / .08) : 0;
  }
  indices[i * 4] = 0; indices[i * 4 + 1] = joint;
  weights[i * 4] = 1 - w; weights[i * 4 + 1] = w;
}
const buffer = root.listBuffers()[0] || doc.createBuffer();
prim.setAttribute('JOINTS_0', doc.createAccessor('winter-joints', buffer).setType('VEC4').setArray(indices));
prim.setAttribute('WEIGHTS_0', doc.createAccessor('winter-weights', buffer).setType('VEC4').setArray(weights));
const inverseBind = new Float32Array(joints.length * 16);
for (let i = 0; i < joints.length; i++) {
  const off = i * 16, t = joints[i].getTranslation();
  inverseBind.set([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, -t[0], -t[1], -t[2], 1], off);
}
const skin = doc.createSkin('WinterSkin').setSkeleton(rigRoot)
  .setInverseBindMatrices(doc.createAccessor('winter-inverse-bind', buffer).setType('MAT4').setArray(inverseBind));
joints.forEach(j => skin.addJoint(j));
const actor = doc.createNode('WinterActor').setMesh(mesh).setSkin(skin);
const scene = root.listScenes()[0];
for (const n of scene.listChildren()) scene.removeChild(n);
scene.addChild(actor).addChild(rigRoot);
if (kind === 'dog') {
  const animation = doc.createAnimation('winter_run'), frames = 25, duration = .65;
  const times = Float32Array.from({ length: frames }, (_, i) => i * duration / (frames - 1));
  const timeAccessor = doc.createAccessor('run-times', buffer).setType('SCALAR').setArray(times);
  for (let leg = 0; leg < 4; leg++) {
    const rotations = new Float32Array(frames * 4), phase = [0, Math.PI, Math.PI, 0][leg];
    for (let i = 0; i < frames; i++) {
      const a = Math.sin(i * Math.PI * 2 / (frames - 1) + phase) * .55;
      rotations.set([Math.sin(a / 2), 0, 0, Math.cos(a / 2)], i * 4);
    }
    const sampler = doc.createAnimationSampler().setInput(timeAccessor)
      .setOutput(doc.createAccessor('run-leg-' + leg, buffer).setType('VEC4').setArray(rotations)).setInterpolation('LINEAR');
    animation.addSampler(sampler).addChannel(doc.createAnimationChannel().setSampler(sampler)
      .setTargetNode(joints[leg + 1]).setTargetPath('rotation'));
  }
}
await doc.transform(prune());
await io.write(output, doc);
console.log(JSON.stringify({ kind, output, joints: joints.length, vertices: count, clips: root.listAnimations().map(a => a.getName()) }));
