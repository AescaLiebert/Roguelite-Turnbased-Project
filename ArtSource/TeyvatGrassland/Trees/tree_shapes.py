"""Deterministic WP05 branch/crown records; no mesh or scene operations.

Coordinates are Blender Z-up metres. The caller normalizes the finished LOD0
height once and reuses these records/that scale for every detail level.
"""
import math
import random
from mathutils import Vector


def records(name, height, width, seed=510731):
    """Return (branches, crowns) in create_tree_kit's existing tuple format.

    branches: (point-tuples, radii); crowns: (center, XYZ radii, stable ID, slot).
    Slots 1/2 are geometric Leaf/LeafLight; the caller adds the spray variants.
    """
    rng = random.Random(seed + sum((i + 1) * ord(c) for i, c in enumerate(name)))
    h = float(height)
    w = float(width)
    branches, crowns = [], []

    def branch(points, radii):
        branches.append(([tuple(p) for p in points], list(radii)))

    def crown(center, radii, light=False):
        crowns.append((tuple(center), tuple(radii), len(crowns) + 101, 2 if light else 1))

    def direction(angle):
        return Vector((math.cos(angle), math.sin(angle), 0))

    def roots(count, radius):
        for j in range(count):
            a = j * math.tau / count + rng.uniform(-.16, .16)
            d = direction(a)
            reach = radius * rng.uniform(.78, 1.15)
            branch([Vector((0, 0, h * .12)),
                    d * reach * .48 + Vector((0, 0, h * .025)),
                    d * reach + Vector((0, 0, .008))],
                   [h * .025, h * .017, .012])

    if "Pine" in name:
        young = name == "YoungPine"
        lean = Vector((h * (.035 if young else -.045), h * .018, 0))
        branch([Vector((0, 0, 0)), lean * .18 + Vector((0, 0, h * .24)),
                lean * .72 + Vector((0, 0, h * .62)),
                lean + Vector((0, 0, h * .985))],
               [h * .036, h * .027, h * .014, .009])
        # Unequal vertical gaps and missing arms make boughs, not a pile of balls.
        levels = [(.26, .26, 4), (.38, .225, 3), (.51, .19, 4),
                  (.65, .145, 3), (.78, .100, 3), (.89, .057, 2)]
        turn = rng.uniform(0, math.tau)
        for level, (z, spread, arms) in enumerate(levels):
            for arm in range(arms):
                a = turn + arm * math.tau / arms + level * .91 + rng.uniform(-.22, .22)
                d = direction(a)
                reach = h * spread * w * rng.uniform(.76, 1.14)
                start = lean * z + Vector((0, 0, h * (z + rng.uniform(-.012, .012))))
                elbow = start + d * reach * .56 + Vector((0, 0, -h * .018))
                end = start + d * reach + Vector((0, 0, h * rng.uniform(.005, .033)))
                branch([start, elbow, end],
                       [h * .012 * (1 - z * .65), h * .0045, .007])
                # A long thin inner bough and an offset small tip overlap softly.
                radial, cross = reach * .34, reach * .22
                radii = (abs(d.x) * radial + abs(d.y) * cross,
                         abs(d.y) * radial + abs(d.x) * cross,
                         h * rng.uniform(.026, .043) * (1 - z * .30) * 1.45)
                c = start + d * reach * .61 + Vector((0, 0, h * .015))
                crown(c, radii, (level + arm) % 5 == 0)
                crown(end - d * reach * .06 + Vector((0, 0, h * .009)),
                      (reach * .16, reach * .14, h * .025 * (1 - z * .3) * 1.20),
                      (level + arm) % 4 == 0)
        crown(lean * .98 + Vector((0, 0, h * .954)),
              (h * .035, h * .032, h * .045), True)
        return branches, crowns

    if name == "Shrub":
        # Three unequal overlapping groups; avoid a ring of equal small spheres.
        groups = [(-.30, -.06, .47, .37, .34, .34),
                  (.19, .16, .66, .43, .37, .36),
                  (.38, -.21, .38, .31, .29, .26)]
        for j, (x, y, z, rx, ry, rz) in enumerate(groups):
            c = Vector((x * h, y * h, z * h))
            branch([Vector((0, 0, 0)), c * .42 + Vector((0, 0, h * .08)), c],
                   [h * .035, h * .020, .009])
            crown(c, (h * rx, h * ry, h * rz), j == 1)
            offset = Vector((h * (.13 if j != 0 else -.13), -h * .11, -h * .13))
            crown(c + offset, (h * rx * .62, h * ry * .70, h * rz * .63), j == 2)
        return branches, crowns

    hero = name == "HeroOak"
    island = name == "IslandTree"
    # The island tree leans along +X above its planted base; meadow oak stays
    # compact and upright. Hero has a crooked S line and four unequal limbs.
    bend = Vector((h * (.13 if island else -.06 if hero else .025),
                   h * (.035 if island else -.018), 0))
    trunk = [Vector((0, 0, 0)), Vector((h * .018, -h * .012, h * .16)),
             bend * .45 + Vector((0, 0, h * .34)),
             bend * .78 + Vector((0, 0, h * .53)),
             bend + Vector((0, 0, h * .73))]
    branch(trunk, [h * (.065 if hero else .048), h * .049, h * .036,
                   h * .023, h * .010])

    if hero:
        # angle, horizontal reach, endpoint height, group radius, start height
        limbs = [(3.35, .405, .63, .167, .31),
                 (1.52, .335, .86, .142, .40),
                 (.12, .435, .72, .182, .35),
                 (5.12, .285, .94, .125, .48)]
    elif island:
        limbs = [(3.42, .245, .65, .145, .35),
                 (.20, .365, .73, .162, .40),
                 (1.66, .225, .88, .128, .48)]
    else:
        limbs = [(3.31, .300, .70, .170, .35),
                 (.32, .305, .73, .163, .39),
                 (1.73, .235, .88, .150, .48)]

    for j, (angle, reach, z, radius, startz) in enumerate(limbs):
        d = direction(angle)
        tangent = direction(angle + math.pi * .5)
        start = bend * (startz / .73) + Vector((0, 0, h * startz))
        end = bend + d * (h * reach * w) + Vector((0, 0, h * z))
        elbow = start.lerp(end, .46) + tangent * h * (.025 if j % 2 else -.035)
        elbow.z -= h * .025
        shoulder = start.lerp(end, .76) - tangent * h * .014
        branch([start, elbow, shoulder, end],
               [h * (.028 if hero else .021), h * .019, h * .012, h * .0045])

        # Each primary limb ends in a group, with two unequal secondary forks.
        # Smaller lobes sit above/beside rather than a regular radial crown ring.
        crown(end, (h * radius * w, h * radius * .84 * w, h * radius * .72), j % 3 == 1)
        for fork in range(2):
            sign = -1 if fork == 0 else 1
            lateral = h * (radius * (.65 if fork == 0 else .48))
            c = end - d * h * radius * .27 + tangent * sign * lateral
            c.z += h * (-.055 if fork == 0 else .065) * (1 if hero else .75)
            base = elbow.lerp(shoulder, .55 if fork == 0 else .80)
            middle = base.lerp(c, .57) - d * h * .018
            branch([base, middle, c], [h * .012, h * .006, .0035])
            size = radius * (.70 if fork == 0 else .58)
            crown(c, (h * size * w, h * size * rng.uniform(.80, 1.10) * w,
                      h * size * rng.uniform(.70, .90)), (j + fork) % 4 == 0)

    if hero:
        # Overlap the four limb fans above their forks; keep the lower branch
        # windows open while avoiding four detached puffs from the review view.
        for j, p in enumerate([(-.18, -.07, .72), (.16, .05, .70), (0, .08, .80)]):
            crown(Vector((p[0] * h, p[1] * h, p[2] * h)),
                  (h * .20 * w, h * .18 * w, h * .17), j == 2)

    # One small bridging group softens the trunk junction without filling all
    # the negative space between branch fans; the crown outline stays unequal.
    crown(bend * .8 + Vector((0, 0, h * (.80 if hero else .83))),
          (h * (.145 if hero else .14) * w, h * .13 * w, h * .13), True)
    roots(5 if hero else 4, h * (.12 if hero else .085))
    return branches, crowns
