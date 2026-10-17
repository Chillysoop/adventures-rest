import type { Zone } from '../content.ts';
import { Importance } from '../ledger.ts';
import { Town } from '../town.ts';
import { CONSTRUCTION_WAGE } from './brain.ts';
import { findLot, nameFor } from './property.ts';

/**
 * Stand-in for the player while the sim runs headless: zones land as it fills and commissions
 * charter houses when the homeless pile up. The real game replaces this with player input.
 */
export function daily(town: Town): void {
  keepZoned(town, 'Residential', 3, 7.5, true);
  keepZoned(town, 'Commercial', 2, 6.5, true);
  keepZoned(town, 'Farmland', 2, 99, false);
  commissionHousing(town);
}

function keepZoned(town: Town, zone: Zone, minVacant: number, maxDist: number, preferNear: boolean): void {
  const vacant = town.lots.filter((l) => l.zone === zone && l.isVacant && l.ownerId === null).length;
  const key = (d: number): number => (preferNear ? d : Math.abs(d - 5.5));
  town.lots
    .filter((l) => {
      const d = Town.distanceFromCenter(l);
      return l.zone === 'None' && d <= maxDist && (preferNear || d >= 5);
    })
    .sort((x, y) => key(Town.distanceFromCenter(x)) - key(Town.distanceFromCenter(y)) || x.id - y.id)
    .slice(0, Math.max(0, minVacant - vacant))
    .forEach((l) => {
      l.zone = zone;
    });
}

function commissionHousing(town: Town): void {
  const homeless = town.residents.filter((a) => a.homeId === null).length;
  const building = town.buildings.some((b) => b.isSite && b.ownerId === null && b.def.housing > 0);
  if (homeless < 5 || building) return;

  const house = town.content.building('house');
  const lot = findLot(town, 'Residential');
  if (!lot) return;
  const cost = house.cost + town.market.quote('Timber', house.timber);
  if (town.treasury < cost + 50) return;

  town.treasury -= cost;
  town.market.buy('Timber', house.timber);
  const site = town.addBuilding(house, lot, null, nameFor(town, house, null), false);
  site.laborBudget = house.buildHours * CONSTRUCTION_WAGE;
  town.log('Town', Importance.Routine, `With ${homeless} people sleeping rough, the council commissions ${site.name}.`);
}
