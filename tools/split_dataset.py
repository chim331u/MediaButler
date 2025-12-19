#!/usr/bin/env python3
"""
Split training dataset into train/test sets (80/20 split)
Stratified by category to maintain class distribution
"""

import csv
import random
from collections import defaultdict

def split_dataset(input_file, train_file, test_file, train_ratio=0.8, seed=42):
    """Split dataset with stratification by category"""
    random.seed(seed)

    # Read all samples
    samples_by_category = defaultdict(list)

    with open(input_file, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            category = row['Category']
            samples_by_category[category].append(row)

    # Split each category
    train_samples = []
    test_samples = []

    category_stats = []

    for category, samples in sorted(samples_by_category.items()):
        # Shuffle samples for this category
        random.shuffle(samples)

        # Calculate split point
        n_total = len(samples)
        n_train = int(n_total * train_ratio)

        # Split
        train_samples.extend(samples[:n_train])
        test_samples.extend(samples[n_train:])

        category_stats.append({
            'category': category,
            'total': n_total,
            'train': n_train,
            'test': n_total - n_train
        })

    # Shuffle again to mix categories
    random.shuffle(train_samples)
    random.shuffle(test_samples)

    # Write train set
    with open(train_file, 'w', encoding='utf-8', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=['FileName', 'Category'])
        writer.writeheader()
        writer.writerows(train_samples)

    # Write test set
    with open(test_file, 'w', encoding='utf-8', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=['FileName', 'Category'])
        writer.writeheader()
        writer.writerows(test_samples)

    # Print statistics
    print(f"Dataset Split Report")
    print(f"=" * 80)
    print(f"Total samples: {sum(s['total'] for s in category_stats)}")
    print(f"Training samples: {len(train_samples)}")
    print(f"Test samples: {len(test_samples)}")
    print(f"Number of categories: {len(category_stats)}")
    print(f"\nPer-Category Breakdown:")
    print(f"-" * 80)
    print(f"{'Category':<30} {'Total':>8} {'Train':>8} {'Test':>8}")
    print(f"-" * 80)

    for stat in sorted(category_stats, key=lambda x: -x['total']):
        print(f"{stat['category']:<30} {stat['total']:>8} {stat['train']:>8} {stat['test']:>8}")

    print(f"-" * 80)
    print(f"{'TOTAL':<30} {sum(s['total'] for s in category_stats):>8} {len(train_samples):>8} {len(test_samples):>8}")

    return len(train_samples), len(test_samples), len(category_stats)

if __name__ == '__main__':
    train_count, test_count, n_categories = split_dataset(
        'data/training_samples.csv',
        'data/train_split.csv',
        'data/test_split.csv',
        train_ratio=0.8,
        seed=42
    )

    print(f"\n✅ Split complete!")
    print(f"   Training: data/train_split.csv ({train_count} samples)")
    print(f"   Test: data/test_split.csv ({test_count} samples)")
    print(f"   Categories: {n_categories}")
