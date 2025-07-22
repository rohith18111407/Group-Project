import { Component, OnInit } from '@angular/core';
import { DashboardService } from '../services/dashboard.service';
import { ChartConfiguration } from 'chart.js';
import { CommonModule } from '@angular/common';
import { NgChartsModule } from 'ng2-charts';

@Component({
  selector: 'app-dashboard',
  imports: [CommonModule, NgChartsModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css'
})
export class DashboardComponent implements OnInit {
  username: string = '';
  loading = false;
  error: string | null = null;

  itemCards: any[] = [];
  
  pieLabels: string[] = [];
  pieData: number[] = [];

  constructor(private dashboardService: DashboardService) { }

  ngOnInit(): void {
    const userJson = sessionStorage.getItem('user');
    this.username = userJson ? JSON.parse(userJson).username : 'User';

    this.loadDashboardData();
  }

  loadDashboardData(): void {
    this.loading = true;
    this.error = null;

    Promise.all([
      this.dashboardService.getAllFiles().toPromise(),
      this.dashboardService.getAllItems().toPromise()
    ])
      .then(([files, items]) => {
        this.prepareCards(items ?? [], files ?? []);
        this.preparePieChart(files ?? []);
        this.loading = false;
      })
      .catch(() => {
        this.error = 'Failed to load dashboard data.';
        this.loading = false;
      });
  }

  prepareCards(items: any[], files: any[]): void {
    const grouped: Record<string, { fileCount: number; totalSize: number; categorySet: Set<string> }> = {};

    for (const item of items) {
      const itemName = item.name;
      const relatedFiles = files.filter(f => f.itemId === item.id);
      const totalSize = relatedFiles.reduce((acc, f) => acc + f.fileSizeInBytes, 0);

      if (!grouped[itemName]) {
        grouped[itemName] = {
          fileCount: relatedFiles.length,
          totalSize: totalSize,
          categorySet: new Set(item.categories || [])
        };
      } else {
        grouped[itemName].fileCount += relatedFiles.length;
        grouped[itemName].totalSize += totalSize;
        for (const cat of item.categories || []) {
          grouped[itemName].categorySet.add(cat);
        }
      }
    }

    this.itemCards = Object.entries(grouped)
      .map(([name, data]) => ({
        name,
        fileCount: data.fileCount,
        categoryCount: data.categorySet.size,
        totalSizeMB: (data.totalSize / (1024 * 1024)).toFixed(2)
      }))
      .reverse(); // latest item name appears first
  }

  preparePieChart(files: any[]): void {
    const categorySizeMap: Record<string, number> = {};
    for (const file of files) {
      const category = file.category || 'Uncategorized';
      categorySizeMap[category] = (categorySizeMap[category] || 0) + file.fileSizeInBytes;
    }

    this.pieLabels = Object.keys(categorySizeMap);
    this.pieData = Object.values(categorySizeMap).map(bytes =>
      parseFloat((bytes / (1024 * 1024)).toFixed(2)) // Convert to MB
    );
  }

  get pieChartData(): ChartConfiguration<'pie'>['data'] {
    return {
      labels: this.pieLabels.map((label, i) => `${label}: ${this.pieData[i]} MB`),
      datasets: [{
        data: this.pieData,
        backgroundColor: this.generateColors(this.pieLabels.length)
      }]
    };
  }

  generateColors(count: number): string[] {
    const baseColors = ['#007bff', '#dc3545', '#ffc107', '#28a745', '#6610f2', '#20c997'];
    return Array.from({ length: count }, (_, i) => baseColors[i % baseColors.length]);
  }
}