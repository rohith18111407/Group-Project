import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class DashboardService {
    constructor(private http: HttpClient) { }
    private readonly baseUrl = 'http://localhost:5239/api/v1';


    getAllFiles(): Observable<any[]> {
        const token = sessionStorage.getItem('jwtToken');
        const headers = new HttpHeaders({ 'Authorization': `Bearer ${token}` });
        return this.http
            .get<any>(`${this.baseUrl}/files`, { headers: headers })
            .pipe(map(res => res.data));
    }

    getAllItems(
        sortBy: 'createdAt' | 'createdBy' | 'name' | 'category' = 'createdAt',
        isDescending: boolean = false
    ): Observable<any[]> {
        const token = sessionStorage.getItem('jwtToken');
        const headers = new HttpHeaders({ 'Authorization': `Bearer ${token}` });

        const params = new URLSearchParams({
            isDescending: isDescending.toString(),
            pageNumber: '1',
            pageSize: '100',
            sortBy
        });

        return this.http
            .get<any>(`${this.baseUrl}/Items?${params.toString()}`, { headers })
            .pipe(map(res => res.data));
    }
}
