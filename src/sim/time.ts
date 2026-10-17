/**
 * Time model for real-time-with-pause: the client advances the simulation one hour per step
 * at whatever speed the player chooses (or not at all while paused). Strategic systems run on the daily tick.
 */
export const HOURS_PER_DAY = 24;

export const hourOf = (tick: number): number => tick % HOURS_PER_DAY;
export const dayOf = (tick: number): number => Math.floor(tick / HOURS_PER_DAY) + 1;

export const isNight = (hour: number): boolean => hour < 6 || hour >= 23;
export const isWorkHours = (hour: number): boolean => hour >= 7 && hour < 17;
export const isEvening = (hour: number): boolean => hour >= 17 && hour < 23;
