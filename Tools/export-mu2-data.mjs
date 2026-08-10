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

const [classModule, unitModule, enemyModule, campaignModule, warmapModule] = await Promise.all([
  import(moduleUrl('src/data/classes.js')),
  import(moduleUrl('src/data/units.js')),
  import(moduleUrl('src/data/demo.js')),
  import(moduleUrl('src/data/campaign.js')),
  import(moduleUrl('src/data/warmaps.js')),
]);

const { CLASSES, CLASS_BY_ID } = classModule;
const { UNITS, UNIT_BY_ID } = unitModule;
const { ENEMY_UNITS } = enemyModule;
const { STAGES } = campaignModule;
const { WARMAPS } = warmapModule;

const attackGrowth = (cls) => Math.max(cls.growth.atk || 0, cls.growth.mag || 0);
const attackRange = (cls) => (cls.traits || []).some((trait) => ['ranged', 'magic'].includes(trait)) ? 2 : 1;
const maxHp = (cls, level) => 24 + cls.growth.hp * Math.max(1, level);
const damage = (cls, level) => 5 + attackGrowth(cls) + Math.floor(Math.max(0, level - 1) * 0.75);

const classRecord = (cls) => ({
  id: cls.id,
  displayName: cls.name,
  role: cls.role,
  hpGrowth: cls.growth.hp || 0,
  attackGrowth: attackGrowth(cls),
  defenseGrowth: cls.growth.def || 0,
  resistanceGrowth: cls.growth.res || 0,
  speedGrowth: cls.growth.spd || 0,
  moveRange: Math.max(1, cls.growth.mov || 1),
  attackRange: attackRange(cls),
  traits: cls.traits || [],
});

const prototypeRecord = (unit, team) => ({
  id: unit.id,
  displayName: unit.name,
  className: unit.className,
  team,
  baseLevel: unit.level,
});

const positionedUnit = (unit, team, level, x, y, instanceId) => {
  const cls = CLASS_BY_ID[unit.className];
  if (!cls) throw new Error(`Unknown class ${unit.className} for ${unit.id}`);
  return {
    id: instanceId,
    sourceUnitId: unit.id,
    displayName: unit.name,
    className: unit.className,
    team,
    level,
    x,
    y,
    maxHp: maxHp(cls, level),
    moveRange: Math.max(1, cls.growth.mov || 1),
    attackRange: attackRange(cls),
    damage: damage(cls, level),
  };
};

const centeredRows = (count) => {
  const rows = {
    1: [3],
    2: [2, 4],
    3: [2, 3, 4],
    4: [1, 2, 4, 5],
    5: [1, 2, 3, 4, 5],
  };
  return rows[count] || Array.from({ length: count }, (_, index) => index + 1);
};

const playerRosterFor = (stageIndex) => [
  UNIT_BY_ID.hero,
  UNIT_BY_ID.azuki,
  UNIT_BY_ID.partner,
  ...(stageIndex >= 1 ? [UNIT_BY_ID.memory1] : []),
  ...(stageIndex >= 2 ? [UNIT_BY_ID.memory2] : []),
];

const stageRecord = (stage, stageIndex) => {
  const players = playerRosterFor(stageIndex);
  const playerRows = centeredRows(players.length);
  const enemyRows = centeredRows(stage.enemy.members.length);
  const recommendedLevel = stageIndex + 1;
  const playerUnits = players.map((unit, index) => positionedUnit(
    unit,
    'player',
    Math.max(unit.level, recommendedLevel),
    1,
    playerRows[index],
    unit.id,
  ));
  const enemyUnits = stage.enemy.members.map((member, index) => positionedUnit(
    member.unit,
    'enemy',
    member.unit.level,
    7,
    enemyRows[index],
    `${stage.id}_${member.unit.id}_${index + 1}`,
  ));

  return {
    id: stage.id,
    displayName: stage.name,
    sourceStageId: stage.id,
    sourceWarmapId: '',
    chapter: stage.chapter,
    backgroundId: stage.bg,
    learningObjective: stage.learn,
    recommendedLevel,
    difficultyIndex: stageIndex + 1,
    width: 9,
    height: 7,
    units: [...playerUnits, ...enemyUnits],
  };
};

const catalog = {
  schemaVersion: 2,
  classes: CLASSES.map(classRecord),
  unitPrototypes: [
    ...UNITS.map((unit) => prototypeRecord(unit, 'player')),
    ...ENEMY_UNITS.map((unit) => prototypeRecord(unit, 'enemy')),
  ],
  stages: STAGES.map(stageRecord),
  warmaps: WARMAPS.map((warmap) => ({
    id: warmap.id,
    displayName: warmap.name,
    difficulty: warmap.difficulty,
    backgroundId: warmap.bg,
    laneCount: warmap.lanes,
    nodeCount: warmap.nodes.length,
    enemySquadCount: warmap.enemy.length,
  })),
};

fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, `${JSON.stringify(catalog, null, 2)}\n`, 'utf8');
console.log(`Exported ${catalog.classes.length} classes, ${catalog.unitPrototypes.length} unit prototypes, ${catalog.stages.length} stages and ${catalog.warmaps.length} warmaps to ${outputPath}`);
