import fs from 'node:fs';
import path from 'node:path';

const inputPath = path.resolve(process.argv[2] || 'Assets/Resources/Data/mu2_content.json');
const catalog = JSON.parse(fs.readFileSync(inputPath, 'utf8'));

const fail = (message) => { throw new Error(message); };
if (catalog.schemaVersion !== 2) fail('schemaVersion must be 2');
if (catalog.classes.length !== 7) fail(`expected 7 classes, got ${catalog.classes.length}`);
if (catalog.unitPrototypes.length !== 18) fail(`expected 18 unit prototypes, got ${catalog.unitPrototypes.length}`);
if (catalog.stages.length !== 6) fail(`expected 6 stages, got ${catalog.stages.length}`);
if (catalog.warmaps.length !== 3) fail(`expected 3 warmaps, got ${catalog.warmaps.length}`);

const classIds = new Set(catalog.classes.map((item) => item.id));
const prototypeIds = new Set(catalog.unitPrototypes.map((item) => item.id));
if (classIds.size !== catalog.classes.length) fail('class ids must be unique');
if (prototypeIds.size !== catalog.unitPrototypes.length) fail('unit prototype ids must be unique');

let previousEnemyScore = -1;
for (const [index, stage] of catalog.stages.entries()) {
  if (stage.difficultyIndex !== index + 1) fail(`${stage.id}: invalid difficultyIndex`);
  const ids = new Set();
  const positions = new Set();
  const teams = new Set();
  for (const unit of stage.units) {
    if (!classIds.has(unit.className)) fail(`${stage.id}: unknown class ${unit.className}`);
    if (!prototypeIds.has(unit.sourceUnitId)) fail(`${stage.id}: unknown source unit ${unit.sourceUnitId}`);
    if (ids.has(unit.id)) fail(`${stage.id}: duplicate unit id ${unit.id}`);
    ids.add(unit.id);
    const position = `${unit.x},${unit.y}`;
    if (positions.has(position)) fail(`${stage.id}: occupied position ${position}`);
    positions.add(position);
    if (unit.x < 0 || unit.x >= stage.width || unit.y < 0 || unit.y >= stage.height) fail(`${stage.id}: unit outside grid`);
    if (unit.maxHp < 1 || unit.damage < 1 || unit.moveRange < 1 || unit.attackRange < 1) fail(`${stage.id}: invalid combat stats`);
    teams.add(unit.team);
  }
  if (!teams.has('player') || !teams.has('enemy')) fail(`${stage.id}: both teams are required`);
  const enemyScore = stage.units.filter((unit) => unit.team === 'enemy')
    .reduce((sum, unit) => sum + unit.maxHp + unit.damage * 3, 0);
  if (enemyScore <= previousEnemyScore) fail(`${stage.id}: enemy difficulty must increase stage by stage`);
  previousEnemyScore = enemyScore;
}

console.log(`Validated M-U2 content: ${catalog.classes.length} classes, ${catalog.unitPrototypes.length} prototypes, ${catalog.stages.length} playable stages, ${catalog.warmaps.length} warmaps.`);
