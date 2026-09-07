"""Read the aggregate Cobertura coverage rate and enforce the CI minimum."""
from decimal import Decimal, InvalidOperation
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def coverage_rate(path):
    root = ET.parse(path).getroot()
    if root.tag != 'coverage':
        raise ValueError('expected a Cobertura coverage document')
    rate = Decimal(root.attrib['line-rate'])
    if not rate.is_finite() or not 0 <= rate <= 1:
        raise ValueError('invalid aggregate line coverage')
    return rate


def main(path):
    try:
        rate = coverage_rate(Path(path))
    except (OSError, ET.ParseError, KeyError, InvalidOperation, ValueError):
        print('::error::Missing or invalid aggregate coverage report')
        return 1
    print(f'Line coverage: {rate * 100:.1f}%')
    if rate < Decimal('0.90'):
        print('::error::Line coverage is below the 90% minimum')
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1]))
