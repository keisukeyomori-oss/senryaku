import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const args = process.argv.slice(2);
const valueAfter = (flag) => {
  const index = args.indexOf(flag);
  if (index < 0 || !args[index + 1]) throw new Error(`Missing ${flag}`);
  return path.resolve(args[index + 1]);
};

const webRoot = valueAfter('--web-root');
const outputPath = valueAfter('--out');
const moduleUrl = (relativePath) => pathToFileURL(path.join(webRoot, relativePath)).href;

const [{ UNIT_BY_ID }, { ENEMY_BY_ID }, { CLASS_BY_ID }, { WARMAP_BY_ID }] = await Promise.all([
  import(moduleUrl('src/data/units.js')),
  import(moduleUrl('src/data/demo.js')),
  import(moduleUrl('src/data/classes.js')),
  import(moduleUrl('src/data/warmaps.js')),
]);

const sourceMap = WARMAP_BY_ID.w1;
const projectUnit = (unit, team, x, y) => {
  const cls = CLASS_BY_ID[unit.className];
  const primaryAttack = Math.max(cls.growth.atk || 0, cls.growth.mag || 0);
  return {
    id: unit.id,
    displayName: unit.name,
    className: unit.className,
    team,
    x,
    y,
    maxHp: 24 + cls.growth.hp * Math.max(1, unit.level),
    moveRange: Math.max(1, cls.growth.mov),
    attackRange: (cls.traits || []).some((trait) => ['ranged', 'magic'].includes(trait)) ? 2 : 1,
    damage: 5 + primaryAttack,
  };
};

const stage = {
  id: 'mu1-border-road',
  displayName: `${sourceMap.name}・訓練戦`,
  sourceWarmapId: sourceMap.id,
  width: 7,
  height: 5,
  units: [
    projectUnit(UNIT_BY_ID.hero, 'player', 1, 2),
    projectUnit(ENEMY_BY_ID.e_knight, 'enemy', 5, 2),
  ],
};

fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, `${JSON.stringify(stage, null, 2)}\n`, 'utf8');
console.log(`Exported ${stage.units.length} units from ${sourceMap.id} to ${outputPath}`);
