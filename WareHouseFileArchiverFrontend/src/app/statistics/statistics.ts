import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { NgChartsModule } from 'ng2-charts';
import { StatisticsService } from '../services/statistics.service';
import { ChartConfiguration } from 'chart.js';

@Component({
  selector: 'app-statistics',
  imports: [CommonModule, NgChartsModule],
  templateUrl: './statistics.html',
  styleUrl: './statistics.css'
})
export class StatisticsComponent implements OnInit{
  fileStats: any[] = [];
  uploadDownloadData: any = {};
  recentActivities: any[] = [];
  recentFiles: any[] = [];
  recentItems: any[] = [];

  constructor(private statisticsService: StatisticsService) {}

  ngOnInit(): void {
    this.statisticsService.getFileExtensionStats().subscribe(res => this.fileStats = res);
    this.statisticsService.getUploadDownloadTrends().subscribe(res => this.uploadDownloadData = res);
    this.statisticsService.getRecentActivities().subscribe(res => this.recentActivities = res);
    this.statisticsService.getRecentFiles().subscribe(res => this.recentFiles = res);
    this.statisticsService.getRecentItems().subscribe(res => this.recentItems = res);
  }

  get pieChartData(): ChartConfiguration<'pie'> {
    return {
      type: 'pie',
      data: {
        labels: this.fileStats.map(f => f.extension),
        datasets: [{
          data: this.fileStats.map(f => f.totalSizeMB),
          backgroundColor: ['#0d6efd', '#6610f2', '#198754', '#dc3545', '#ffc107']
        }]
      }
    };
  }

  get lineChartData(): ChartConfiguration<'line'> {
    return {
      type: 'line',
      data: {
        labels: this.uploadDownloadData?.days || [],
        datasets: [
          {
            label: 'Uploads',
            data: this.uploadDownloadData?.uploads || [],
            borderColor: '#0d6efd',
            fill: false,
          },
          {
            label: 'Downloads',
            data: this.uploadDownloadData?.downloads || [],
            borderColor: '#198754',
            fill: false,
          }
        ]
      }
    };
  }
}
